# NewLife.Agent - 守护服务管理组件

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.agent?logo=github)
![GitHub License](https://img.shields.io/github/license/newlifex/newlife.agent?logo=github)
![Nuget Downloads](https://img.shields.io/nuget/dt/newlife.agent?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/newlife.agent?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/newlife.agent?label=dev%20nuget&logo=nuget)
![Nuget Downloads](https://img.shields.io/nuget/dt/NewLife.Extensions.Hosting.AgentService?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/NewLife.Extensions.Hosting.AgentService?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/NewLife.Extensions.Hosting.AgentService?label=dev%20nuget&logo=nuget)

使用教程：[https://newlifex.com/core/agent](https://newlifex.com/core/agent)  

# 功能特点
NewLife.Agent是一个服务管理框架，用于开发随系统自动启动的长时间运行后台应用程序，支持Windows/Linux。  
在Windows上注册为Windows服务，在Linux上注册为Systemd守护进程。  
Agent支持`netstandard2.0/net45/net40/mono`，旧版本还支持net20（已不再维护）。  
源码库：[https://github.com/NewLifeX/NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent)  

Agent常用于各种后台应用，如aspnetcore应用、RPC网络服务器、MQTT服务器、数据处理应用、数据消费应用（Redis/RocketMQ）等。同类软件有 NSSM、srvany，但并不一样，Agent是框架，而它们是具体软件，更像星尘代理 StarAgent。  

NewLife.Agent主要功能：  
1. 注册应用为系统服务或守护进程，随系统自动启动
1. 支持控制台菜单控制安装、卸载、启动、停止，以及查看状态
1. 支持控制台调试应用，解决Windows服务难以调试的问题
1. 支持健康检测，限制内存、线程数、句柄数，超限时重启应用服务
1. 支持应用服务定时重启，通过配置指定
1. 支持看门狗WatchDog，通过配置指定要守护的目标应用服务，如果目标停止则启动
1. 支持配置文件修改服务名，一个应用程序可在多个目录上部署为不同的系统服务

# 服务控制
一个服务代理示例跑起来的样子  
![image.png](https://cdn.nlark.com/yuque/0/2020/png/1144030/1585307461258-ca6e7880-b2ef-4d87-aed7-35bace08a149.png#align=left&display=inline&height=202&name=image.png&originHeight=404&originWidth=926&size=60071&status=done&style=none&width=463)  
![image.png](https://cdn.nlark.com/yuque/0/2020/png/1144030/1585547897654-f6b570c5-cbfd-4f4e-86c2-a9d711a53c58.png#align=left&display=inline&height=207&name=image.png&originHeight=414&originWidth=1129&size=60665&status=done&style=none&width=564.5)  
这是Agent的标准控制台（Windows和Centos）。上面是该服务的状态信息，下面是控制菜单。  
示例分析：  
- 服务名 XAgent/StarAgent，可以命令启动停止，Windows是`net start XAgent/net stop XAgent`，Linux是`systemctl start StarAgent/systemctl stop StarAgent`。
- 显示名“新生命服务代理”是在windows服务控制板里面看到的名字
- 下一段信息给出了NewLife.Agent和当前应用的版本信息和编译时间
- 黄色菜单可通过按键选择相应操作，内置012345，可自定义其它按键操作
- 菜单1，显示状态，按下1后刷新状态信息
- 菜单2，安装服务或卸载服务，安装成功后，显示信息变为卸载服务，反之亦然
- 菜单3，启动服务或停止服务，安装后才可以看见
- 菜单4，重启服务，安装且运行后可以看见
- 菜单5，模拟运行，在当前进程启动应用主逻辑，用于业务逻辑调试，等同于Windows服务调用
- 菜单0，退出应用服务

# 关于net8+的使用
MyServices8+.cs、Program8+.cs是net8+的demo，暂时是注释的，有需要可以参考

**！！！注意，服务安装、卸载、启动、停止，在Windows/Linux上需要管理员权限运行**  

服务应用在Windows上以本地用户权限运行，有最高权限；  
服务应用在Linux上以root权限运行，有最高权限；  
该设计尽管带来了一定安全风险，但能够避免绝大部分初级用户的简单问题，优先易用性。  

## 新生命项目矩阵
各项目默认支持net9.0/netstandard2.1/netstandard2.0/net4.62/net4.5，旧版（2024.0801）支持net4.0/net2.0  

|                               项目                               | 年份  | 说明                                                                                   |
| :--------------------------------------------------------------: | :---: | -------------------------------------------------------------------------------------- |
|                             基础组件                             |       | 支撑其它中间件以及产品项目                                                             |
|          [NewLife.Core](https://github.com/NewLifeX/X)           | 2002  | 核心库，日志、配置、缓存、网络、序列化、APM性能追踪                                    |
|    [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode)    | 2005  | 大数据中间件，单表百亿级，MySql/SQLite/SqlServer/Oracle/PostgreSql/达梦，自动分表      |
|      [NewLife.Net](https://github.com/NewLifeX/NewLife.Net)      | 2005  | 网络库，单机千万级吞吐率（2266万tps），单机百万级连接（400万Tcp）                      |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011  | RPC通信框架，内网高吞吐，物联网设备低开销易接入                                        |
|     [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube)     | 2010  | 魔方快速开发平台，集成了用户权限、SSO登录、OAuth服务端等，单表100亿级项目验证          |
|    [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent)    | 2008  | 服务管理组件，把应用安装成为操作系统守护进程，Windows服务、Linux的Systemd              |
|     [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero)     | 2020  | Zero零代脚手架，基于NewLife组件生态的项目模板NewLife.Templates，Web、WebApi、Service   |
|                              中间件                              |       | 对接知名中间件平台                                                                     |
|    [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)    | 2017  | Redis客户端，微秒级延迟，百万级吞吐，丰富的消息队列，百亿级数据量项目验证              |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018  | RocketMQ纯托管客户端，支持Apache RocketMQ和阿里云消息队列，十亿级项目验                |
|     [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT)     | 2019  | 物联网消息协议，MqttClient/MqttServer，客户端支持阿里云物联网                          |
|      [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT)      | 2022  | IoT标准库，定义物联网领域的各种通信协议标准规范                                        |
|   [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus)   | 2022  | ModbusTcp/ModbusRTU/ModbusASCII，基于IoT标准库实现，支持IoT平台和IoTEdge               |
|  [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens)  | 2022  | 西门子PLC协议，基于IoT标准库实现，支持IoT平台和IoTEdge                                 |
|      [NewLife.Map](https://github.com/NewLifeX/NewLife.Map)      | 2022  | 地图组件库，封装百度地图、高德地图、腾讯地图、天地图                                   |
|    [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio)    | 2023  | 音频编解码库，PCM/ADPCMA/G711A/G722U/WAV/AAC                                           |
|                             产品平台                             |       | 产品平台级，编译部署即用，个性化自定义                                                 |
|         [Stardust](https://github.com/NewLifeX/Stardust)         | 2018  | 星尘，分布式服务平台，节点管理、APM监控中心、配置中心、注册中心、发布中心              |
|           [AntJob](https://github.com/NewLifeX/AntJob)           | 2019  | 蚂蚁调度，分布式大数据计算平台（实时/离线），蚂蚁搬家分片思想，万亿级数据量项目验证    |
|      [NewLife.ERP](https://github.com/NewLifeX/NewLife.ERP)      | 2021  | 企业ERP，产品管理、客户管理、销售管理、供应商管理                                      |
|         [CrazyCoder](https://github.com/NewLifeX/XCoder)         | 2006  | 码神工具，众多开发者工具，网络、串口、加解密、正则表达式、Modbus                       |
|           [EasyIO](https://github.com/NewLifeX/EasyIO)           | 2023  | 简易文件存储，支持分布式系统中文件集中存储。                                           |
|           [XProxy](https://github.com/NewLifeX/XProxy)           | 2005  | 产品级反向代理，NAT代理、Http代理                                                      |
|        [HttpMeter](https://github.com/NewLifeX/HttpMeter)        | 2022  | Http压力测试工具                                                                       |
|         [GitCandy](https://github.com/NewLifeX/GitCandy)         | 2015  | Git源代码管理系统                                                                      |
|          [SmartOS](https://github.com/NewLifeX/SmartOS)          | 2014  | 嵌入式操作系统，完全独立自主，支持ARM Cortex-M芯片架构                                 |
|          [SmartA2](https://github.com/NewLifeX/SmartA2)          | 2019  | 嵌入式工业计算机，物联网边缘网关，高性能.NET6主机，应用于工业、农业、交通、医疗        |
|                          FIoT物联网平台                          | 2020  | 物联网整体解决方案，建筑、环保、农业，软硬件及大数据分析一体化，单机十万级点位项目验证 |
|                        UWB高精度室内定位                         | 2020  | 厘米级（10~20cm）高精度室内定位，软硬件一体化，与其它系统联动，大型展厅项目验证        |

## 新生命开发团队
![XCode](https://newlifex.com/logo.png)  

新生命团队（NewLife）成立于2002年，是新时代物联网行业解决方案提供者，致力于提供软硬件应用方案咨询、系统架构规划与开发服务。  
团队主导的80多个开源项目已被广泛应用于各行业，Nuget累计下载量高达300余万次。  
团队开发的大数据中间件NewLife.XCode、蚂蚁调度计算平台AntJob、星尘分布式平台Stardust、缓存队列组件NewLife.Redis以及物联网平台FIoT，均成功应用于电力、高校、互联网、电信、交通、物流、工控、医疗、文博等行业，为客户提供了大量先进、可靠、安全、高质量、易扩展的产品和系统集成服务。  

我们将不断通过服务的持续改进，成为客户长期信赖的合作伙伴，通过不断的创新和发展，成为国内优秀的IoT服务供应商。  

`新生命团队始于2002年，部分开源项目具有20年以上漫长历史，源码库保留有2010年以来所有修改记录`  
网站：https://newlifex.com  
开源：https://github.com/newlifex  
QQ群：1600800/1600838  
微信公众号：  
![智能大石头](https://newlifex.com/stone.jpg)  
