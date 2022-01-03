using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using System;

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
        public static void MethodEntry(ILogger logger, ManagementJobConfiguration jobConfig, string name)
        {
            logger.LogTrace($"[Host:{jobConfig.CertificateStoreDetails.ClientMachine}][Store:{jobConfig.CertificateStoreDetails.StorePath}] Entered '{name}' method.");
        }
        public static void MethodExit(ILogger logger, ManagementJobConfiguration jobConfig, string name)
        {
            logger.LogTrace($"[Host:{jobConfig.CertificateStoreDetails.ClientMachine}][Store:{jobConfig.CertificateStoreDetails.StorePath}] Leaving '{name}' method.");
        }
        public static void Info(ILogger logger, ManagementJobConfiguration jobConfig, string message)
        {
            logger.LogInformation($"[Host:{jobConfig.CertificateStoreDetails.ClientMachine}][Store:{jobConfig.CertificateStoreDetails.StorePath}] {message}");
        }
        public static void Error(ILogger logger, ManagementJobConfiguration jobConfig, string message)
        {
            logger.LogError($"[Host:{jobConfig.CertificateStoreDetails.ClientMachine}][Store:{jobConfig.CertificateStoreDetails.StorePath}] {message}");
        }
        public static void Warn(ILogger logger, ManagementJobConfiguration jobConfig, string message)
        {
            logger.LogWarning($"[Host:{jobConfig.CertificateStoreDetails.ClientMachine}][Store:{jobConfig.CertificateStoreDetails.StorePath}] {message}");
        }
        public static void Debug(ILogger logger, ManagementJobConfiguration jobConfig, string message)
        {
            logger.LogDebug($"[Host:{jobConfig.CertificateStoreDetails.ClientMachine}][Store:{jobConfig.CertificateStoreDetails.StorePath}] {message}");
        }
        public static void Trace(ILogger logger, ManagementJobConfiguration jobConfig, string message)
        {
            logger.LogTrace($"[Host:{jobConfig.CertificateStoreDetails.ClientMachine}][Store:{jobConfig.CertificateStoreDetails.StorePath}] {message}");
        }
    }
}
