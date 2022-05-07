﻿using System.Diagnostics;
using System.Text;
using NewLife.Log;

namespace NewLife.Agent
{
    /// <summary>Linux版进程守护</summary>
    public class RcInit : Host
    {
        #region 静态
        private static readonly String _path;

        /// <summary>是否可用</summary>
        public static Boolean Available => !_path.IsNullOrEmpty();

        /// <summary>实例化</summary>
        static RcInit()
        {
            var ps = new[] {
                "/etc/rc.d/init.d",
            };
            foreach (var p in ps)
            {
                if (Directory.Exists(p))
                {
                    _path = p;
                    break;
                }
            }
        }
        #endregion

        #region 属性
        #endregion

        /// <summary>启动服务</summary>
        /// <param name="service"></param>
        public override void Run(ServiceBase service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            // 以服务运行
            InService = true;

            try
            {
                // 启动初始化
                service.StartLoop();

                // 阻塞
                service.DoLoop();

                // 停止
                service.StopLoop();
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }

        /// <summary>服务是否已安装</summary>
        /// <param name="serviceName">服务名</param>
        /// <returns></returns>
        public override Boolean IsInstalled(String serviceName)
        {
            var file = _path.CombinePath($"{serviceName}");

            return File.Exists(file);
        }

        /// <summary>服务是否已启动</summary>
        /// <param name="serviceName">服务名</param>
        /// <returns></returns>
        public override Boolean IsRunning(String serviceName)
        {
            var pid = $"{serviceName}.pid".GetFullPath();

            return File.Exists(pid);
        }

        /// <summary>安装服务</summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="displayName">显示名</param>
        /// <param name="binPath">文件路径</param>
        /// <param name="description">描述信息</param>
        /// <returns></returns>
        public override Boolean Install(String serviceName, String displayName, String binPath, String description) => Install(_path, serviceName, binPath, displayName, description);

        /// <summary>安装服务</summary>
        /// <param name="systemdPath">systemd目录有</param>
        /// <param name="serviceName">服务名</param>
        /// <param name="displayName">显示名</param>
        /// <param name="binPath">文件路径</param>
        /// <param name="description">描述信息</param>
        /// <returns></returns>
        public static Boolean Install(String systemdPath, String serviceName, String binPath, String displayName, String description)
        {
            XTrace.WriteLine("{0}.Install {1}, {2}, {3}, {4}", typeof(Systemd).Name, serviceName, displayName, binPath, description);

            var file = systemdPath.CombinePath($"{serviceName}");
            XTrace.WriteLine(file);

            var des = !displayName.IsNullOrEmpty() ? displayName : description;

            var sb = new StringBuilder();
            sb.AppendLine("#!/bin/bash");
            sb.AppendLine("# chkconfig: 2345 10 90");
            sb.AppendLine($"# description: {des}");

            sb.AppendLine();
            sb.AppendLine($"cd {".".GetFullPath()}");
            sb.AppendLine(binPath);
            sb.AppendLine($"exit 0");

            File.WriteAllText(file, sb.ToString());

            return true;
        }

        /// <summary>卸载服务</summary>
        /// <param name="serviceName">服务名</param>
        /// <returns></returns>
        public override Boolean Remove(String serviceName)
        {
            XTrace.WriteLine("{0}.Remove {1}", GetType().Name, serviceName);

            var file = _path.CombinePath($"{serviceName}");
            if (File.Exists(file)) File.Delete(file);

            return true;
        }

        /// <summary>启动服务</summary>
        /// <param name="serviceName">服务名</param>
        /// <returns></returns>
        public override Boolean Start(String serviceName)
        {
            XTrace.WriteLine("{0}.Start {1}", GetType().Name, serviceName);

            var file = _path.CombinePath($"{serviceName}");
            var p = Process.Start("bash", file);

            // 用pid文件记录进程id，方便后面杀进程
            var pid = $"{serviceName}.pid".GetFullPath();
            File.WriteAllText(pid, p.ToString());

            return true;
        }

        /// <summary>停止服务</summary>
        /// <param name="serviceName">服务名</param>
        /// <returns></returns>
        public override Boolean Stop(String serviceName)
        {
            XTrace.WriteLine("{0}.Stop {1}", GetType().Name, serviceName);

            var id = 0;
            var pid = $"{serviceName}.pid".GetFullPath();
            if (File.Exists(pid)) id = File.ReadAllText(pid).Trim().ToInt();
            if (id <= 0) return false;

            // 杀进程
            var p = Process.GetProcessById(id);
            if (p == null) return false;

            try
            {
                p.Kill();

                File.Delete(pid);

                return true;
            }
            catch { }

            return false;
        }

        /// <summary>重启服务</summary>
        /// <param name="serviceName">服务名</param>
        public override Boolean Restart(String serviceName)
        {
            XTrace.WriteLine("{0}.Restart {1}", GetType().Name, serviceName);

            Stop(serviceName);
            Start(serviceName);

            return true;
        }
    }
}