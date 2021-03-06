﻿using System;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using NewLife.Agent;

namespace Zero.Web
{
    public class Program
    {
        private static void Main(String[] args) => new MyServices { Args = args }.Main(args);
    }

    /// <summary>代理服务例子。自定义服务程序可参照该类实现。</summary>
    public class MyServices : ServiceBase
    {
        #region 属性
        /// <summary>性能跟踪器</summary>
        public String[] Args { get; set; }
        #endregion

        #region 构造函数
        /// <summary>实例化一个代理服务</summary>
        public MyServices()
        {
            // 一般在构造函数里面指定服务名
            ServiceName = "WebAgent";

            DisplayName = "Web服务代理";
            Description = "用于承载各种服务的服务代理！";
        }
        #endregion

        #region 核心
        private CancellationTokenSource _source;
        /// <summary>开始工作</summary>
        /// <param name="reason"></param>
        protected override void StartWork(String reason)
        {
            WriteLog("业务开始……");

            _source = new CancellationTokenSource();
            CreateHostBuilder(Args).Build().RunAsync(_source.Token);

            base.StartWork(reason);
        }

        public static IHostBuilder CreateHostBuilder(String[] args) =>
            Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        /// <summary>停止服务</summary>
        /// <param name="reason"></param>
        protected override void StopWork(String reason)
        {
            WriteLog("业务结束！{0}", reason);

            _source.Cancel();

            base.StopWork(reason);
        }
        #endregion
    }
}