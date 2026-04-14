* WSM Monitor User Guide

This guide provides instructions on how to install, configure, and troubleshoot the WSM Monitor application. For a general overview of the project, refer to the README file.

* Overview

WSM Monitor is a Windows utility that collects system metrics and displays them through a web dashboard. It can operate as a standard tray application or as a persistent Windows service. The companion application allows for status monitoring and configuration management.

* Installation

The application can be installed using one of the following methods.

1. Installer Method.
Use Inno Setup 6 to build the installer using the provided script in the installer and build-installer.bat files. Run the resulting setup executable to install the application on your system.

2. Portable Method.
Copy WSMMonitor.exe from the publish folder after running the build script. Ensure that the appsettings.json file and the rules folder are placed in the same directory as the executable.
In companion mode, the application may not require installation, you can build a portable version through build-wsm-exe.cmd.

* Initial Setup

1. Launch WSMMonitor.exe with no command line arguments to start the tray companion.
2. Access the status window by double clicking the tray icon or using the application menu.
3. Use the Open web dashboard option to view metrics in your browser at the default local address.

If the Windows service is not installed and the application is set to Companion mode, the tray app will run an internal metrics agent.

* Windows Service Management

For continuous background monitoring, the application should be registered as a Windows service using an administrative PowerShell session.

To install and start the service:
cd "C:\Program Files\WSM Monitor"
.\WSMMonitor.exe --install-service
sc start WSMMonitor

To stop and remove the service:
sc stop WSMMonitor
.\WSMMonitor.exe --uninstall-service

* Configuration

Settings can be managed through the application interface. Changes are saved to appsettings.local.json, which overrides the default configuration. A restart of the application or service is required after modifying the port or work mode.

Work Modes:
1. Service Mode. The companion communicates with the background service agent.
2. Companion Mode. The tray application runs its own embedded agent while active.

Configuration can also be managed via environment variables using the WSMMONITOR prefix.

* HTTP API Reference

The application provides a local API at the /api/v1/ prefix.

GET /health. Checks if the process is responding.
GET /ready. Confirms that the first metrics collection is complete.
GET /api/v1/metrics. Provides a full snapshot of current system data.
GET /api/v1/agent-status. Displays version, process ID, and current operational mode.
GET /metrics. Provides data in Prometheus format.

* Security and Sigma Rules

The application utilizes Sigma rules located in the rules directory. For security event monitoring, the Microsoft Windows Sysmon Operational log must be available on the system. Suppressions can be configured in the suppressions.json file.

* Troubleshooting

1. Service fails to start. Check the Windows Event Viewer under Application logs for the WSMMonitor source. Common issues include the port being occupied by another process or lack of write permissions in the installation folder.
2. Agent unreachable. Verify that the Windows service is running using the sc query command and ensure the port settings match between the companion and the agent.
3. Dashboard version mismatch. An older version of the service may be running. Stop the service, replace the executable with the current version, and restart.
4. Language issues. The dashboard language is typically controlled by the companion application settings.

* Command Line Arguments

The executable supports the following parameters:

No arguments. Starts the tray companion.
--service. Runs the process as a background service.
--install-service. Registers the application as a Windows service.
--uninstall-service. Removes the Windows service registration.
--export-icon. Extracts the application icon to a file.

