using Common.Logging;
using CSS.Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator
{
    internal class ExceptionHandler
    {
        public static string FlattenExceptionMessages(Exception ex, string message)
        {
            if (ex is F5RESTException)
            {
                message += " " + ((F5RESTException)ex).message + Environment.NewLine;
            }
            else
            {
                message += " " + ex.Message + Environment.NewLine;
            }
            if (ex.InnerException != null)
            {
                message = FlattenExceptionMessages(ex.InnerException, message);
            }

            return message;
        }
    }

    internal class F5RESTException : Exception
    {
        #region Properties from F5
        public int code { get; set; }
        public string[] errorStack { get; set; }
        public int apiError { get; set; }
        public string message { get; set; }
        #endregion

        public string RequestString { get; set; }
        public bool IsPost { get; set; } = false;
        public string RequestBody { get; set; }
    }

    internal class LogHandler
    {
        public static void MethodEntry(ILog logger, AnyJobConfigInfo jobConfig, string name)
        {
            logger.Trace($"[Host:{jobConfig.Store.ClientMachine}][Store:{jobConfig.Store.StorePath}] Entered '{name}' method.");
        }
        public static void MethodExit(ILog logger, AnyJobConfigInfo jobConfig, string name)
        {
            logger.Trace($"[Host:{jobConfig.Store.ClientMachine}][Store:{jobConfig.Store.StorePath}] Leaving '{name}' method.");
        }
        public static void Info(ILog logger, AnyJobConfigInfo jobConfig, string message)
        {
            logger.Info($"[Host:{jobConfig.Store.ClientMachine}][Store:{jobConfig.Store.StorePath}] {message}");
        }
        public static void Error(ILog logger, AnyJobConfigInfo jobConfig, string message)
        {
            logger.Error($"[Host:{jobConfig.Store.ClientMachine}][Store:{jobConfig.Store.StorePath}] {message}");
        }
        public static void Warn(ILog logger, AnyJobConfigInfo jobConfig, string message)
        {
            logger.Warn($"[Host:{jobConfig.Store.ClientMachine}][Store:{jobConfig.Store.StorePath}] {message}");
        }
        public static void Debug(ILog logger, AnyJobConfigInfo jobConfig, string message)
        {
            logger.Debug($"[Host:{jobConfig.Store.ClientMachine}][Store:{jobConfig.Store.StorePath}] {message}");
        }
        public static void Trace(ILog logger, AnyJobConfigInfo jobConfig, string message)
        {
            logger.Trace($"[Host:{jobConfig.Store.ClientMachine}][Store:{jobConfig.Store.StorePath}] {message}");
        }
    }
}
