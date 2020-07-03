using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewLife.Agent;
using NewLife.Log;
using NewLife.Threading;

namespace Test
{
    public class MyServices:MyXService
    {
        private TimerX _timer;
        protected override void StartWork(String reason)
        {
            // 5秒开始，每10秒执行一次
            _timer = new TimerX(DoWork, null, 5_000, 10_000) { Async = true };
        }


        /// <summary>
        /// 实际负责的工作
        /// </summary>
        /// <param name="state"></param>
        private void DoWork(Object state)
        {
            string data = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            //日志会输出到BinTest目录中
            XTrace.WriteLine($"代码执行时间：{data}");
        }
    }
}
