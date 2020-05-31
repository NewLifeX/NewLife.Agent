# 服务管理NewLife.Agent
使用教程：[https://www.yuque.com/smartstone/nx/agent](https://www.yuque.com/smartstone/nx/agent)  

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
- 菜单5，循环调试，在当前进程启动应用主逻辑，用于业务逻辑调试，等同于Windows服务调用
- 菜单0，退出应用服务

**！！！注意，服务安装、卸载、启动、停止，在Windows/Linux上需要管理员权限运行**  

服务应用在Windows上以本地用户权限运行，有最高权限；  
服务应用在Linux上以root权限运行，有最高权限；  
该设计尽管带来了一定安全风险，但能够避免绝大部分初级用户的简单问题，优先易用性。  

## 新生命开源项目矩阵
各项目默认支持netstandard2.0/net4.5/net4.0/  

|                               项目                               | 年份  |  状态  | .NET Core | 说明                                                |
| :--------------------------------------------------------------: | :---: | :----: | :-------: | --------------------------------------------------- |
|                             基础组件                             |       |        |           | 支撑其它中间件以及产品项目                          |
|          [NewLife.Core](https://github.com/NewLifeX/X)           | 2002  | 维护中 |     √     | 算法、日志、网络、RPC、序列化、缓存、多线程         |
|              [XCode](https://github.com/NewLifeX/X)              | 2005  | 维护中 |     √     | 数据中间件，MySQL、SQLite、SqlServer、Oracle        |
|      [NewLife.Net](https://github.com/NewLifeX/NewLife.Net)      | 2005  | 维护中 |     √     | 网络库，千万级吞吐率，学习gRPC、Thrift              |
|     [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube)     | 2010  | 维护中 |     √     | Web魔方，企业级快速开发框架，集成OAuth              |
|    [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent)    | 2008  | 维护中 |     √     | 服务管理框架，Windows服务、Linux的Systemd           |
|                              中间件                              |       |        |           | 对接各知名中间件平台                                |
|    [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)    | 2017  | 维护中 |     √     | Redis客户端，微秒级延迟，百亿级项目验证             |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018  | 维护中 |     √     | 支持Apache RocketMQ和阿里云消息队列，十亿级项目验证 |
|     [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT)     | 2019  | 维护中 |     √     | 物联网消息协议，客户端支持阿里云物联网              |
|     [NewLife.LoRa](https://github.com/NewLifeX/NewLife.LoRa)     | 2016  | 维护中 |     √     | 超低功耗的物联网远程通信协议LoRaWAN                 |
|   [NewLife.Thrift](https://github.com/NewLifeX/NewLife.Thrift)   | 2019  | 维护中 |     √     | Thrift协议实现                                      |
|     [NewLife.Hive](https://github.com/NewLifeX/NewLife.Hive)     | 2019  | 维护中 |     √     | 纯托管读写Hive，Hadoop数据仓库，基于Thrift协议      |
|             [NoDb](https://github.com/NewLifeX/NoDb)             | 2017  | 开发中 |     √     | NoSQL数据库，百万级kv读写性能，持久化               |
|      [NewLife.Ftp](https://github.com/NewLifeX/NewLife.Ftp)      | 2008  | 维护中 |     √     | Ftp客户端实现                                       |
|                             产品平台                             |       |        |           | 产品平台级，编译部署即用，个性化自定义              |
|           [AntJob](https://github.com/NewLifeX/AntJob)           | 2019  | 维护中 |     √     | 蚂蚁调度系统，大数据实时计算平台                    |
|         [Stardust](https://github.com/NewLifeX/Stardust)         | 2018  | 维护中 |     √     | 星尘，微服务平台，分布式平台                        |
|            [XLink](https://github.com/NewLifeX/XLink)            | 2016  | 维护中 |     √     | 物联网云平台                                        |
|           [XProxy](https://github.com/NewLifeX/XProxy)           | 2005  | 维护中 |     √     | 产品级反向代理                                      |
|          [XScript](https://github.com/NewLifeX/XScript)          | 2010  | 维护中 |     ×     | C#脚本引擎                                          |
|          [SmartOS](https://github.com/NewLifeX/SmartOS)          | 2014  | 维护中 |   C++11   | 嵌入式操作系统，完全独立自主，ARM Cortex-M芯片架构  |
|         [GitCandy](https://github.com/NewLifeX/GitCandy)         | 2015  | 维护中 |     ×     | Git管理系统                                         |
|                               其它                               |       |        |           |                                                     |
|           [XCoder](https://github.com/NewLifeX/XCoder)           | 2006  | 维护中 |     √     | 码神工具，开发者必备                                |
|        [XTemplate](https://github.com/NewLifeX/XTemplate)        | 2008  | 维护中 |     √     | 模版引擎，T4(Text Template)语法                     |
|       [X组件 .NET2.0](https://github.com/NewLifeX/X_NET20)       | 2002  | 存档中 |  .NET2.0  | 日志、网络、RPC、序列化、缓存、Windows服务、多线程  |

#### 新生命开发团队
`新生命团队始于2002年，部分开源项目具有15年以上漫长历史，源码库保留有2010年以来所有修改记录，并一直保持更新，请确保获取得到最新版本源代码`  
网站：https://www.NewLifeX.com  
开源：https://github.com/NewLifeX  
博客：https://nnhy.cnblogs.com  
QQ群：1600800/1600838  
