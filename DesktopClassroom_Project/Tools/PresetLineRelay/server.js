#!/usr/bin/env node

const http = require("http");
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const url = require("url");

const PORT = Number(process.env.PORT || 8787);
const RELAY_TOKEN = process.env.RELAY_TOKEN || "";
const PUBLIC_DIR = __dirname;
const rooms = new Map();

function sendHttp(res, status, body, contentType = "text/plain; charset=utf-8") {
  res.writeHead(status, {
    "content-type": contentType,
    "cache-control": "no-store",
  });
  res.end(body);
}

const server = http.createServer((req, res) => {
  const parsed = url.parse(req.url, true);

  if (parsed.pathname === "/" || parsed.pathname === "/console") {
    const html = fs.readFileSync(path.join(PUBLIC_DIR, "researcher_console.html"), "utf8");
    sendHttp(res, 200, html, "text/html; charset=utf-8");
    return;
  }

  if (parsed.pathname === "/health") {
    sendHttp(res, 200, JSON.stringify({ ok: true, rooms: rooms.size }), "application/json");
    return;
  }

  sendHttp(res, 404, "Not found");
});

server.on("upgrade", (req, socket) => {
  const parsed = url.parse(req.url, true);
  if (parsed.pathname !== "/ws") {
    socket.destroy();
    return;
  }

  if (RELAY_TOKEN && parsed.query.token !== RELAY_TOKEN) {
    socket.write("HTTP/1.1 401 Unauthorized\r\n\r\n");
    socket.destroy();
    return;
  }

  const key = req.headers["sec-websocket-key"];
  if (!key) {
    socket.destroy();
    return;
  }

  const accept = crypto
    .createHash("sha1")
    .update(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")
    .digest("base64");

  socket.write([
    "HTTP/1.1 101 Switching Protocols",
    "Upgrade: websocket",
    "Connection: Upgrade",
    `Sec-WebSocket-Accept: ${accept}`,
    "\r\n",
  ].join("\r\n"));

  const client = createClient(socket, parsed.query);
  addClient(client);
});

function createClient(socket, query) {
  const client = {
    id: crypto.randomBytes(4).toString("hex"),
    socket,
    role: String(query.role || "unknown"),
    room: String(query.room || "demo"),
    buffer: Buffer.alloc(0),
    alive: true,
  };

  socket.on("data", (chunk) => {
    client.buffer = Buffer.concat([client.buffer, chunk]);
    parseFrames(client);
  });
  socket.on("close", () => removeClient(client));
  socket.on("error", () => removeClient(client));

  return client;
}

function addClient(client) {
  if (!rooms.has(client.room)) {
    rooms.set(client.room, new Set());
  }

  rooms.get(client.room).add(client);
  broadcastStatus(client.room);
  console.log(`[relay] ${client.role} ${client.id} joined room=${client.room}`);
}

function removeClient(client) {
  if (!client.alive) return;
  client.alive = false;

  const room = rooms.get(client.room);
  if (room) {
    room.delete(client);
    if (room.size === 0) {
      rooms.delete(client.room);
    } else {
      broadcastStatus(client.room);
    }
  }

  try {
    client.socket.destroy();
  } catch {
    // Ignore socket cleanup errors.
  }

  console.log(`[relay] ${client.role} ${client.id} left room=${client.room}`);
}

function parseFrames(client) {
  while (client.buffer.length >= 2) {
    const first = client.buffer[0];
    const second = client.buffer[1];
    const opcode = first & 0x0f;
    const masked = Boolean(second & 0x80);
    let payloadLength = second & 0x7f;
    let offset = 2;

    if (payloadLength === 126) {
      if (client.buffer.length < offset + 2) return;
      payloadLength = client.buffer.readUInt16BE(offset);
      offset += 2;
    } else if (payloadLength === 127) {
      if (client.buffer.length < offset + 8) return;
      const high = client.buffer.readUInt32BE(offset);
      const low = client.buffer.readUInt32BE(offset + 4);
      payloadLength = high * 2 ** 32 + low;
      offset += 8;
    }

    const maskOffset = offset;
    if (masked) offset += 4;
    if (client.buffer.length < offset + payloadLength) return;

    const frameBuffer = client.buffer;
    let payload = frameBuffer.slice(offset, offset + payloadLength);
    client.buffer = frameBuffer.slice(offset + payloadLength);

    if (masked) {
      const mask = frameBuffer.slice(maskOffset, maskOffset + 4);
      const unmasked = Buffer.alloc(payload.length);
      for (let i = 0; i < payload.length; i += 1) {
        unmasked[i] = payload[i] ^ mask[i % 4];
      }
      payload = unmasked;
    }

    if (opcode === 0x8) {
      removeClient(client);
      return;
    }

    if (opcode === 0x9) {
      sendFrame(client, payload, 0xA);
      continue;
    }

    if (opcode !== 0x1) {
      continue;
    }

    handleMessage(client, payload.toString("utf8"));
  }
}

function handleMessage(sender, text) {
  let message;
  try {
    message = JSON.parse(text);
  } catch {
    sendJson(sender, { type: "error", text: "Invalid JSON" });
    return;
  }

  if (message.type === "hello") {
    sendJson(sender, {
      type: "hello_ack",
      room: sender.room,
      role: sender.role,
      clients: getRoomClientSummary(sender.room),
    });
    return;
  }

  const forwarded = {
    ...message,
    forwardedBy: sender.role,
    serverTimestamp: Date.now() / 1000,
  };

  const room = rooms.get(sender.room);
  if (!room) return;

  for (const client of room) {
    if (!client.alive || client === sender) continue;
    sendJson(client, forwarded);
  }

  console.log(`[relay] ${sender.room} ${sender.role}: ${message.type || "message"}`);
}

function broadcastStatus(roomName) {
  const room = rooms.get(roomName);
  if (!room) return;

  const payload = {
    type: "room_status",
    room: roomName,
    clients: getRoomClientSummary(roomName),
  };

  for (const client of room) {
    sendJson(client, payload);
  }
}

function getRoomClientSummary(roomName) {
  const room = rooms.get(roomName);
  if (!room) return [];

  return Array.from(room).map((client) => ({
    id: client.id,
    role: client.role,
  }));
}

function sendJson(client, payload) {
  sendFrame(client, Buffer.from(JSON.stringify(payload), "utf8"), 0x1);
}

function sendFrame(client, payload, opcode = 0x1) {
  if (!client.alive || client.socket.destroyed) return;

  const length = payload.length;
  let header;
  if (length < 126) {
    header = Buffer.from([0x80 | opcode, length]);
  } else if (length <= 0xffff) {
    header = Buffer.alloc(4);
    header[0] = 0x80 | opcode;
    header[1] = 126;
    header.writeUInt16BE(length, 2);
  } else {
    header = Buffer.alloc(10);
    header[0] = 0x80 | opcode;
    header[1] = 127;
    header.writeUInt32BE(0, 2);
    header.writeUInt32BE(length, 6);
  }

  try {
    client.socket.write(Buffer.concat([header, payload]));
  } catch {
    removeClient(client);
  }
}

server.listen(PORT, () => {
  console.log(`[relay] listening on http://0.0.0.0:${PORT}`);
  if (RELAY_TOKEN) {
    console.log("[relay] RELAY_TOKEN is enabled.");
  }
});
