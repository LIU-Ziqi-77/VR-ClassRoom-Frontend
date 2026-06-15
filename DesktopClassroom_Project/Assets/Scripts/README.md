# 学生Avatar行为控制系统

## 功能概述

这个系统为VR教师培训项目提供了完整的学生Avatar行为控制功能，包括：

- **TTS语音合成**：学生可以说话
- **唇形同步**：说话时嘴唇会相应动作
- **眼神追踪**：可以看向指定位置（教师、其他学生等）
- **行为控制**：举手、做笔记、困惑等行为
- **网络通信**：支持后台实时控制

## 系统架构

### 核心组件

1. **TTSService** - TTS语音合成服务
2. **LipSyncController** - 唇形同步控制器
3. **EyeController** - 眼神追踪控制器
4. **StudentBehaviorController** - 学生行为主控制器
5. **StudentNetworkManager** - 网络通信管理器
6. **StudentTestController** - 测试控制器

## 安装和配置

### 1. 设置TTS服务

在场景中创建TTSService对象：

```csharp
// 在TTSService组件中设置API密钥
TTSService.Instance.apiKey = "your_azure_tts_key";
```

支持的TTS服务：
- Azure Cognitive Services (推荐)
- 其他支持SSML的TTS服务

### 2. 配置学生Avatar

为每个学生Avatar添加以下组件：

```csharp
// 必需组件
- StudentBehaviorController
- LipSyncController  
- EyeController
- VRMBlendShapeProxy
- VRMHumanoid
- Animator
```

### 3. 设置学生信息

在StudentBehaviorController中配置：

```csharp
studentId = "student_001";
studentName = "小明";
```

### 4. 配置BlendShape名称

根据你的VRM模型调整BlendShape名称：

```csharp
// 唇形BlendShape
aBlendShapeName = "A";
iBlendShapeName = "I";
uBlendShapeName = "U";
eBlendShapeName = "E";
oBlendShapeName = "O";

// 眼神BlendShape
lookUpBlendShapeName = "LookUp";
lookDownBlendShapeName = "LookDown";
lookLeftBlendShapeName = "LookLeft";
lookRightBlendShapeName = "LookRight";
blinkBlendShapeName = "Blink";
```

## 使用方法

### 1. 基本控制

```csharp
// 获取学生控制器
StudentBehaviorController student = FindObjectOfType<StudentBehaviorController>();

// 让学生说话
await student.SpeakWithLipSync("大家好，我是学生小明");

// 看向教师
student.LookAtTeacher();

// 看向其他学生
student.LookAtStudent("student_002");

// 看向指定位置
student.LookAtPosition(new Vector3(0, 1.6f, 2f));

// 设置行为
student.SetBehavior(StudentBehaviorType.RaisingHand, 3f);
```

### 2. 网络控制

通过WebSocket发送JSON命令：

```json
{
    "studentId": "student_001",
    "type": "Speak",
    "text": "老师，我有一个问题"
}
```

```json
{
    "studentId": "student_001", 
    "type": "LookAt",
    "targetStudentId": "student_002"
}
```

```json
{
    "studentId": "student_001",
    "type": "Behavior", 
    "behaviorType": "RaisingHand",
    "duration": 3.0
}
```

### 3. 测试功能

使用StudentTestController进行测试：

- **键盘快捷键**：
  - 1-4：选择学生
  - 空格：说话
  - T：看向教师
  - R：举手
  - X：重置
  - B：随机行为
  - S：随机说话
  - A：所有学生说话

## 支持的行为类型

```csharp
public enum StudentBehaviorType
{
    Idle,           // 空闲
    Speaking,       // 说话
    Listening,      // 听讲
    RaisingHand,    // 举手
    TakingNotes,    // 做笔记
    LookingAround,  // 环顾四周
    Confused,       // 困惑
    Excited         // 兴奋
}
```

## 网络通信协议

### 消息格式

所有消息都是JSON格式：

```json
{
    "type": "command_type",
    "studentId": "student_id",
    "data": {...}
}
```

### 命令类型

1. **Speak** - 说话
2. **LookAt** - 看向目标
3. **Behavior** - 设置行为
4. **Gesture** - 手势
5. **Stop** - 停止当前行为

### 状态反馈

系统会自动发送状态反馈：

```json
{
    "type": "status",
    "studentId": "student_001",
    "status": "speaking",
    "timestamp": "2024-01-01T12:00:00Z"
}
```

## 性能优化建议

1. **TTS缓存**：缓存常用的语音片段
2. **BlendShape优化**：减少不必要的BlendShape更新
3. **网络优化**：使用消息队列避免消息丢失
4. **内存管理**：及时释放音频资源

## 故障排除

### 常见问题

1. **TTS不工作**
   - 检查API密钥是否正确
   - 确认网络连接正常
   - 查看Console错误信息

2. **唇形同步不准确**
   - 调整analysisWindow参数
   - 检查BlendShape名称是否正确
   - 优化音频分析算法

3. **眼神追踪不工作**
   - 确认眼球骨骼是否正确
   - 检查BlendShape名称
   - 调整maxLookAngle参数

4. **网络连接失败**
   - 检查服务器地址
   - 确认WebSocket服务运行
   - 查看网络日志

## 扩展开发

### 添加新的行为类型

1. 在StudentBehaviorType枚举中添加新类型
2. 在StudentBehaviorController中实现对应方法
3. 添加相应的动画和BlendShape控制

### 自定义TTS服务

1. 继承TTSService类
2. 实现GenerateSpeechAsync方法
3. 配置相应的API调用

### 添加新的手势

1. 在Animator中添加新的动画状态
2. 在StudentBehaviorController中添加手势方法
3. 更新网络协议支持新手势

## 技术支持

如有问题，请检查：
1. Unity Console错误信息
2. 网络连接状态
3. VRM模型配置
4. API密钥有效性

## 更新日志

### v1.0.0
- 初始版本发布
- 支持TTS和唇形同步
- 支持眼神追踪
- 支持基本行为控制
- 支持网络通信 