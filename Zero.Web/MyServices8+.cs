//using System;
//using System.ComponentModel;
//using Microsoft.AspNetCore.Builder;
//using NewLife.Agent;
//using NewLife.Configuration;
//using Microsoft.Extensions.Hosting;

//namespace Zero.Web
//{
//    public class MyServices : ServiceBase
//    {
//        public Func<WebApplication> StartAct { get; set; }

//        #region 构造函数
//        /// <summary>实例化一个代理服务</summary>
//        public MyServices()
//        {

//        }

//        protected override void Init()
//        {
//            base.Init();

//            // 依赖网络
//            if (Host is Systemd sys)
//            {
//                sys.Setting.Network = true;
//            }
//        }
//        #endregion

//        #region 核心
//        private System.Threading.CancellationTokenSource _source;
//        /// <summary>开始工作</summary>
//        /// <param name="reason"></param>
//        public override void StartWork(string reason)
//        {
//            //为了避免每个服务都配置成同一端口，在这做个拦截
//            //if (MyServicesSetting.Current.ServiceUrl == "http://localhost:6001")
//            //{
//            //    throw new Exception("请在Config文件夹配置MyService.config的ServiceUrl节点！");
//            //}


//            WriteLog("业务开始……");


//            _source = new System.Threading.CancellationTokenSource();

//            if (StartAct != null)
//            {
//                var app = StartAct.Invoke();
//                app.Urls.Add(MyServicesSetting.Current.ServiceUrl);
//                app.RunAsync(_source.Token);
//            }


//            base.StartWork(reason);
//        }


//        /// <summary>停止服务</summary>
//        /// <param name="reason"></param>
//        public override void StopWork(String reason)
//        {
//            WriteLog("业务结束！{0}", reason);

//            _source.Cancel();

//            base.StopWork(reason);
//        }
//        #endregion
//    }

//    public class MyServicesSetting : Config<MyServicesSetting>
//    {
//        [Description("服务运行地址")]
//        public string ServiceUrl { get; set; } = "http://localhost:6001";
//    }
//}
