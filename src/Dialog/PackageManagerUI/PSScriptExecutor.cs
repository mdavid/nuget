﻿using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using NuGet.OutputWindowConsole;
using NuGet.VisualStudio.Resources;
using NuGetConsole;
using NuGetConsole.Host.PowerShell;

namespace NuGet.Dialog.PackageManagerUI {

    [Export(typeof(IScriptExecutor))]
    public class PSScriptExecutor : IScriptExecutor {

        private readonly Lazy<IPowerShellHost> _psHost;

        public PSScriptExecutor() {
            _psHost = new Lazy<IPowerShellHost>(GetHost);
        }

        private IPowerShellHost PSHost {
            get {
                return _psHost.Value;
            }
        }

        [Import]
        public IOutputConsoleProvider OutputConsoleProvider {
            get;
            set;
        }

        public bool Execute(string installPath, string scriptFileName, IPackage package, EnvDTE.Project project, ILogger logger) {
            string toolsPath = Path.Combine(installPath, "tools");
            string fullPath = Path.Combine(toolsPath, scriptFileName);
            if (File.Exists(fullPath)) {

                string logMessage = String.Format(CultureInfo.CurrentCulture, VsResources.ExecutingScript, fullPath);

                // logging to both the Output window and progress window.
                logger.Log(MessageLevel.Info, logMessage);
                WriteHost(logMessage);

                PSHost.Invoke(
                    "$__pc_args=@(); $input|%{$__pc_args+=$_}; & '" + fullPath + "' $__pc_args[0] $__pc_args[1] $__pc_args[2] $__pc_args[3]; Remove-Variable __pc_args -Scope 0",
                    new object[] { installPath, toolsPath, package, project },
                    outputResults: true);
                return true;
            }
            
            return false;
        }

        private void WriteHost(string message) {
            PSHost.Invoke("Write-Host '" + message.Replace("'", "''") + "'", null, true);
        }

        private IPowerShellHost GetHost() {
            // create the console and instantiate the PS host on demand
            IConsole console = OutputConsoleProvider.CreateOutputConsole(requirePowerShellHost: true);
            IHost host = console.Host;

            // start the console 
            console.Dispatcher.Start();

            // gives the host a chance to do initialization works before dispatching commands to it
            host.Initialize();

            // after the host initializes, it may set IsCommandEnabled = false
            if (host.IsCommandEnabled) {
                // the OutputConsoleProvider guarantees to return PowerShell host, hence this cast is safe
                return (IPowerShellHost)host;
            }
            else {
                // the PowerShell host fails to initialize if group policy restricts loading of scripts
                throw new InvalidOperationException(VsResources.Console_GroupPolicyError);
            }
        }
    }
}