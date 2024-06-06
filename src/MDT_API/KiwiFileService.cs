using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ServiceInterface;
using System.Text;

namespace MDT_API;

public class KiwiFile
{
    public string Name { get; init; }
    public string RelativePath { get; init; }
    public string Description { get; init;}
    public string Documentation { get; init; }
}

public class KiwiFiles
{
    public static Dictionary<string, KiwiFile> DescriptorLookup =>
    new()
    {
        [Mainboard.Name] = Mainboard,
        [Alarms.Name] = Alarms,
        [J1939.Name] = J1939,
        [Simulator.Name] = Simulator,
        [CalculatedSensors.Name] = CalculatedSensors,
        [Powertrains.Name] = Powertrains,
        [Blender.Name] = Blender,
        [Unit.Name] = Unit
    };

    public static readonly KiwiFile Simulator = new KiwiFile()
    {
        Name = "Simulator",
        RelativePath = "etc/simulator.config",
        Description = "Consumed by SimNext to facilitate simulating kiwi.  Also read by kiwi to setup comms with simnext (database info etc)",
        Documentation = ""
    };

    public static readonly KiwiFile Mainboard = new KiwiFile()
    {
        Name = "Mainboard",
        RelativePath = "etc/mainboard.config",
        Description = "Sets up low level systems running on the 3XL/3XM - Analog/Digital sensors, Outputs, CAN Bus handlers and chip setup, Boards, RTOS Task setuo, and Logging",
        Documentation = ""
    };

    public static readonly KiwiFile Alarms = new KiwiFile()
    {
        Name = "Alarms",
        RelativePath = "etc/alarms.config",
        Description = "Split into two parts - Alarms and Constraints.  Alarms are raised when any of their constraints are active.  They can have an action, just be informative, and optionally suppressed from sending to an HMI.  " +
                      "Constraints describe conditions under which an input/system state should be monitored.  They can be SensorReadings, CalculatedReadings, RegisteredReadings, J1939 DM1 messages, Conjunctions/Disjunctions of other constraints." +
                      "Constraints can be used outside of alarms - they can be treated as binary sensors in other components, and are often used to make functionality of powertrain/control systems more extensible" +
                      "Note: many different alarms can be raised by the system - not all alarms that can be raised are defined in this file.",
        Documentation = ""
    };

    public static readonly KiwiFile Unit = new KiwiFile()
    {
        Name = "Unit",
        RelativePath = "etc/unit.config",
        Description = "Sets up unit specific configuration, including a text substitution mechanism that will overwrite values in other config files.  Typically used to set unit name, ip addresses, etc.  Substituted strings start with a '$' in other files.  ie '$RegistrationName' used in eb07.config would map to the 'RegistrationName' defined in this file",
        Documentation = ""
    };

    public static readonly KiwiFile J1939 = new KiwiFile()
    {
        Name = "J1939",
        RelativePath = "etc/j1939.config",
        Description = "Defines all the PGNs and SPN configuration as defined in SAE J1939.  Usually doesn't change unless the system has custom J1939 messaging.  Very large file, not recommended to load as it can consume many tokens to do so",
        Documentation = ""
    };

    public static readonly KiwiFile CalculatedSensors = new KiwiFile()
    {
        Name = "CalculatedSensors",
        RelativePath = "etc/calculatedSensors.config",
        Description = "Configures all the calculated sensors.  These take sensors in and apply a transform to them.  This file maps the input to the output calculated sensor, sets up smoothing (mean/median), how to interpret the input signal, and defines a mapping to the interpretation parameters (these are defined in sensorConfig.json)" +
                      "Further, this file also includes an optional section for defining simple formulas",
        Documentation = ""
    };

    public static readonly KiwiFile SensorConfig = new KiwiFile()
    {
        Name = "SensorConfig",
        RelativePath = "etc/sensorConfig.json",
        Description = "Defines all the sensor interpretation parameters.  These are used by calculated sensors to apply a transform to their input sensor.  Includes things like unit of measure, raw signal range, output sensor range, kfactors.  Oddly uses the nomenclature IPC ID to link as the unique identifier that is used by calculated sensors.",
        Documentation = ""
    };

    public static readonly KiwiFile Powertrains = new KiwiFile()
    {
        Name = "Powertrains",
        RelativePath = "etc/powertrains.config",
        Description = "Configures one or more component based powertrains that are controlled by the system.  Typically this involves a manual control panel, engine, transmission, starter and pump as well as an assortment of components/peripherals like lube systems, fans, clutches, pumping hours calculators, etc",
        Documentation = ""
    };

    public static readonly KiwiFile Blender = new KiwiFile()
    {
        Name = "Blender",
        RelativePath = "etc/blender.config",
        Description = "Configuration for control elements that are not powertrains (those are in powertrains.config).  This can be augers, liquid additives, dry additives, mixers, valves, level controls, teamed pump/auger controls, generalized state machines, a variety of PID controlled systems,  Most components support staging in some capacity (a period of operation with a start and end, generally defined by start and end volume of a monitored rate sensor)",
        Documentation = ""
    };
}

public interface IKiwiFileService
{
    string GetInfoForAllKiwiFiles();
    Task<string> FetchFile(string address, KiwiFile file);
    Task<string> GetSensorValues(string address, string[] filter = null);
}

public class KiwiSystemMessages : ISystemMessageProvider
{
    public const string KiwiIntegrationsSystemMessage =
        @"You are a chat bot that integrates with a control system called 'Kiwi', created by Mobile Data Technologies.  You have several functions at your disposal to do a variety of tasks associated with this control system.  Kiwi is a general purpose control software that is primarily (but not exclusively) used to control industrial equipment in the hydraulic fracturing industry (namely frac pumps, blenders, chemical units, datavans etc).  Do not ask for approval before executing any functions, even if there are several to execute consequtively.  If you start a new session, give it this system message along with any relevant context.";

    public string SystemMessage => KiwiIntegrationsSystemMessage;
}

public class KiwiFileService : IKiwiFileService
{
    private readonly HttpClient httpClient;

    public KiwiFileService()
    {
        httpClient = new();
    }

    public string GetInfoForAllKiwiFiles()
    {
        return JsonConvert.SerializeObject(KiwiFiles.DescriptorLookup);
    }    
    
    public Task<string> FetchFile(string address, KiwiFile file)
    {
        try
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            return client.GetStringAsync($"http://{address}:3000/{file.RelativePath}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Could not fetch {file.Name} file from {address}, see inner exception", ex);
        }
    }

    public async Task<string> GetSensorValues(string address, string[] filter = null)
    {
        object command;

        if (filter != null)
        {
            command = new
            {
                command = new
                {
                    commandId = "REQUEST_CURRENT_CALCULATED_READINGS",
                    sensors = filter
                }
            };
        }
        else
        {
            command = new
            {
                command = new
                {
                    commandId = "REQUEST_CURRENT_CALCULATED_READINGS",
                    sensors = new string[0]
                }
            };
        }

        try
        {
            var json = await SendCommandAsync(address, command);
            return json.ToString();
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    public async Task<JObject> SendCommandAsync(string ip, object command, string endpoint = "commands")
    {
        string url = $"http://{ip}:3000/control/{endpoint}";

        var json = JsonConvert.SerializeObject(command);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        return JObject.Parse(responseContent);
    }
}
