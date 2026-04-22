

<p align="center"><img src="images/crestron_logo_nuget.jpg"></p>
<h1 align="center"> Crestron.SimplSharp.SDK.Program</a></h1>
<p align="center">Crestron's 4-Series appliances and server based Virtual Control enable you to program your system in C#. This package includes all the necessary libraries needed to create a C# Program that runs on 4-Series or Virtual Control.</p>

## Index
- [Quick start](#quick-start)
- [Links](#links)
- [Dependencies](#dependencies)
- [Documentation](#documentation)
- [Release Notes](#release-notes)
- [Contributing](#contributing)
- [License](LICENSE)


## Quick Start

To program your Crestron system in C#, follow these steps:
1. Install the package via NuGet. You can use the following command in the Package Manager Console:
	```
	dotnet add package Crestron.SimplSharp.SDK.Program
	```

2. Import these namespaces in your code file(s) at a minimum:
	```csharp
	using Crestron.SimplSharp;
	using Crestron.SimplSharpPro;
	```
	Depending on your application and the devices used, you might need to import additional namespaces.

3. Create a class that inherits from CrestronControlSystem and add a parameterless constructor:
	The constructor needs to exit in a timely fashion, otherwise the program will exit.
	You cannot send/receive data in the constructor
	```csharp
	public class ControlSystem : CrestronControlSystem
	{
		public ControlSystem() : base()
		{
			try
			{
				// Initialize Crestron threadpool
				Crestron.SimplSharpPro.CrestronThread.Thread.MaxNumberOfUserThreads = 20;
			}
			catch (Exception e)
			{
				ErrorLog.Error("Error in the constructor: {0}", e.Message);
			}
		}
	}
	```
	Use the constructor to:  
	* Initialize the maximum number of threads (max = 400)  	
	* Register devices  
	* Register event handlers  
	* Add Console Commands
	
4. Add InitializeSystem()
	This method gets called after the constructor has finished.
	InitializeSystem() needs to exit in a timely fashion, otherwise the program will exit.
	```csharp
	public override void InitializeSystem()
	{
		try
		{
			
		}
		catch (Exception e)
		{
			ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
		}
	}
	```
	Use InitializeSystem to:  
	* Start threads  
	 * Configure ports, such as serial and verisports  
	 * Start and initialize socket connections  
	 * Send initial device configurations
	 
5. Subscribe to controller events 
 
	**Program Status Events**
	Event handler for programmatic events, such as program stop, pause, and resume.
	Handling the program stopping scenario is mandatory.
	```csharp
	CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControllerProgramEventHandler);
	```
		
	```csharp
	void ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)  
	{  
		switch (programStatusEventType)  
		{  
			case (eProgramStatusEventType.Paused):  
				// The program has been paused. Pause all user threads/timers as needed.  
				break;  
			case (eProgramStatusEventType.Resumed):  
				// The program has been resumed. Resume all the user threads/timers as needed.
				break;  
			case (eProgramStatusEventType.Stopping):  
				// The program has been stopped.  
				// Close all threads.            
				// Shutdown all Client/Servers in the system.  
				// General cleanup. 
				// Unsubscribe to all System Monitor events  
				break;  
		}  
	}
	```
 
	**OPTIONAL: System Events**
	Event handler for system events, such as disk inserted/ejected, and system reboot 
	```csharp
	CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControllerSystemEventHandler);
	```
	
	```csharp
	void ControllerSystemEventHandler(eSystemEventType systemEventType)  
	{  
		switch (systemEventType)  
		 {  
			case (eSystemEventType.DiskInserted):  
				// Removable media was detected on the system  
				break;  
			case (eSystemEventType.DiskRemoved):  
				// Removable media was detached from the system  
				break;  
			case (eSystemEventType.Rebooting):  
				// The system is rebooting.   
				// Very limited time to preform clean up and save any settings to disk.  
				break;  
		 }
	}
	 ```
	 
	**OPTIONAL: Ethernet Events**
	Event handler for ethernet events, such as link up / link down
	```csharp
	CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControllerEthernetEventHandler);
	````
	
	```csharp
	void _ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)  
	{  
		switch (ethernetEventArgs.EthernetEventType)  
		{
			//Determine the event type Link Up or Link Down  
			case (eEthernetEventType.LinkDown):  
				//Next need to determine which adapter the event is for.   
				//LAN is the adapter is the port connected to external networks.  
				if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)  
				{  
				
				}  
				break;  
			case (eEthernetEventType.LinkUp):  
				if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)  
				{
				  
				}  
				break;  
		}
	}
	````

## Links

## Release Notes  

## Documentation
Additional documentation can be found <a href="https://community.crestron.com/s/article/id-1000637">here</a></p>

## License

This project is licensed under the [MIT License](LICENSE.md).