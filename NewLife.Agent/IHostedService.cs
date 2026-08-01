using System;
using System.Threading;
using System.Threading.Tasks;

namespace NewLife.Agent
{
    /// <summary>主机承载的服务</summary>
    public interface IHostedService
    {
        /// <summary>服务名</summary>
        String ServiceName { get; }

        /// <summary>开始</summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>停止</summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task StopAsync(CancellationToken cancellationToken);
    }
}