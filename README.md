WSM Monitor Local

WSM Monitor Local is a system monitoring solution for Windows that provides comprehensive metrics, security tracking, and a browser-based dashboard. The application is designed for privacy, keeping all traffic on the local machine by default.

* Features

Metrics tracking:
Monitoring of CPU, memory, disks, network, processes, services, and Windows events.

Security:
Integration with Sysmon for Sigma rule evaluation and event suppression.

Web Dashboard:
Local interface featuring dark mode, interactive charts, data filtering, and system health scoring.

Integrations:
Support for Prometheus via the metrics endpoint and OpenTelemetry (OTLP) when the exporter endpoint is configured. (in develop)

Management:
Tray companion application for status monitoring, configuration access, and diagnostic data collection.

Operational Modes:
Support for both standalone companion mode and background Windows service mode.

* Quick Start

1. Download the latest version of the application.
2. Run WSMMonitor.exe from the publish folder or use the Windows installer.
3. If run without arguments, the application starts in the system tray. Use the tray menu to open the dashbord.
4. For persistent background monitoring, run the following commands in an administrator PowerShell window:
   .\WSMMonitor.exe --install-service
   sc start WSMMonitor

The local dashboard is accessible at http://127.0.0.1/.

* Requirements

Operating System:
Windows 10, Windows 11, or Windows Server.

Development:
.NET 8 SDK is required to build the project from source.

* Building from Source

To build the executable:
Run the build-wsm-exe.bat script or use the following command:
dotnet build .\WSMMonitor.App\WSMMonitor.App.csproj -c Release

To run tests:
dotnet test .\WSMMonitor.Tests\WSMMonitor.Tests.csproj -c Release

* Windows Installer Generation

The repository includes the necessary files to create a setup.exe using Inno Setup 6.

1. Install Inno Setup 6 on your system.
2. Run installer\build-installer.bat.
3. The script will publish a self-contained win-x64 application into the staging folder.
4. The final installer will be generated in installer\Output\.

Note that the staging and output folders are excluded from the repository via gitignore.

* Security and Privacy

All monitoring data remains on the local host unless an external OTLP exporter is manually configured. The dashboard listener is bound to the local loopback address. If you discover a security vulnerability, please report it privately to the maintainer.

* License

This project is licensed under the MIT License. See the LICENSE file for more details.

---



