// Copyright 2023 Keyfactor                                                   
// Licensed under the Apache License, Version 2.0 (the "License"); you may    
// not use this file except in compliance with the License.  You may obtain a 
// copy of the License at http://www.apache.org/licenses/LICENSE-2.0.  Unless 
// required by applicable law or agreed to in writing, software distributed   
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES   
// OR CONDITIONS OF ANY KIND, either express or implied. See the License for  
// thespecific language governing permissions and limitations under the       
// License. 
﻿using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using System;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator
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

    internal class LogHandlerCommon
    {
        public static void MethodEntry(ILogger logger, CertificateStore certificateStore, string name)
        {
            logger.LogTrace($"[Host:{certificateStore.ClientMachine}][Store:{certificateStore.StorePath}] Entered '{name}' method.");
        }
        public static void MethodExit(ILogger logger, CertificateStore certificateStore, string name)
        {
            logger.LogTrace($"[Host:{certificateStore.ClientMachine}][Store:{certificateStore.StorePath}] Leaving '{name}' method.");
        }
        public static void Info(ILogger logger, CertificateStore certificateStore, string message)
        {
            logger.LogInformation($"[Host:{certificateStore.ClientMachine}][Store:{certificateStore.StorePath}] {message}");
        }
        public static void Error(ILogger logger, CertificateStore certificateStore, string message)
        {
            logger.LogError($"[Host:{certificateStore.ClientMachine}][Store:{certificateStore.StorePath}] {message}");
        }
        public static void Warn(ILogger logger, CertificateStore certificateStore, string message)
        {
            logger.LogWarning($"[Host:{certificateStore.ClientMachine}][Store:{certificateStore.StorePath}] {message}");
        }
        public static void Debug(ILogger logger, CertificateStore certificateStore, string message)
        {
            logger.LogDebug($"[Host:{certificateStore.ClientMachine}][Store:{certificateStore.StorePath}] {message}");
        }
        public static void Trace(ILogger logger, CertificateStore certificateStore, string message)
        {
            logger.LogTrace($"[Host:{certificateStore.ClientMachine}][Store:{certificateStore.StorePath}] {message}");
        }
    }
}
