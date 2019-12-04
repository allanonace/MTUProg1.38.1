﻿using System;
using System.Collections.Generic;
using System.IO;
using Xml;
using Library.Exceptions;
using System.Xml.Serialization;
using Library;
using System.Linq;
using System.Text;

using ActionType = MTUComm.Action.ActionType;

namespace MTUComm
{
    public class Configuration
    {
        private const string XML_MTUS      = "mtu.xml";
        private const string XML_METERS    = "meter.xml";
        public const string XML_GLOBAL     = "global.xml";
        private const string XML_INTERFACE = "Interface.xml";
        private const string XML_ALARMS    = "alarm.xml";
        private const string XML_DEMANDS   = "demandconf.xml";
        private const string XML_USERS     = "user.xml";

        public MtuTypes mtuTypes;
        public MeterTypes meterTypes;
        public Global Global { private set; get; }
        public InterfaceConfig interfaces;
        public AlarmList alarms;
        public DemandConf demands;
        public User[] users;
        
        private string device;
        private string deviceUUID;
        private string version;
        private string appName;
        private static Configuration instance;

        private Configuration ( string path = "", bool avoidXmlError = false )
        {
            string configPath = Mobile.ConfigPath;

            device = "PC";

            try
            {
                // Load configuration files ( xml's )

                mtuTypes   = Utils.DeserializeXml<MtuTypes>        ( Path.Combine ( configPath, XML_MTUS      ) );
                meterTypes = Utils.DeserializeXml<MeterTypes>      ( Path.Combine ( configPath, XML_METERS    ) );
                Global     = Utils.DeserializeXml<Global>          ( Path.Combine ( configPath, XML_GLOBAL    ) );
                alarms     = Utils.DeserializeXml<AlarmList>       ( Path.Combine ( configPath, XML_ALARMS    ) );
                demands    = Utils.DeserializeXml<DemandConf>      ( Path.Combine ( configPath, XML_DEMANDS   ) );
                users      = Utils.DeserializeXml<UserList>        ( Path.Combine ( configPath, XML_USERS     ) ).List;
                interfaces = Utils.DeserializeXml<InterfaceConfig> ( XML_INTERFACE, true ); // From resources
                
                // Preload port types, because some ports use a letter but other a list of Meter IDs
                // Done here because Xml project has no reference to MTUComm ( cross references )
                List<string> portTypes;
                StringBuilder allTypes = new StringBuilder ();
                foreach ( Mtu mtu in mtuTypes.Mtus )
                {
                    foreach ( Port port in mtu.Ports )
                    {
                        bool isNumeric = MtuAux.GetPortTypes ( port.Type, out portTypes );

                        // Some Meters have numeric type ( e.g. 122 ) and some of them appears
                        // twice in meter.xml, one for a Meter ID and other for a Meter type
                        port.IsSpecialCaseNumType = meterTypes.ContainsNumericType ( portTypes[ 0 ] );

                        // Set if this Mtu only supports certain Meter IDs
                        if ( isNumeric &&
                             ! port.IsSpecialCaseNumType )
                            port.CertainMeterIds.AddRange ( portTypes );

                        // Type is string or is an special numeric case ( e.g. 122, 123,... )
                        if ( ! isNumeric ||
                             port.IsSpecialCaseNumType )
                            port.TypeString = string.Join ( string.Empty, portTypes );
                        
                        // Type is a number or list of numbers/IDs supported
                        // Recover Meter searching for the first supported Meter and get its type
                        else
                        {
                            foreach ( string id in portTypes )
                            {
                                string types = meterTypes.FindByMterId ( int.Parse ( id ) ).Type;

                                // Get all different types from all supported Meters
                                // Type 1: ABC
                                // Type 2: DRE
                                // Type 3: MFR
                                // Type 4: ACC
                                // Type 5: ROL
                                // Result: ABCDREMFOL
                                foreach ( char c in types.ToList ().Except ( allTypes.ToString ().ToList () ) )
                                    allTypes.Append ( c );
                            }

                            port.TypeString = allTypes.ToString ();
                            allTypes.Clear ();
                        }
                        
                       // Utils.Print ( "MTU " + mtu.Id + ": Type " + port.TypeString );
                    }
                }
                allTypes = null;

                // Regenerate certificate from base64 string
                Mobile.configData.GenerateCertFromStore();
                //Mobile.configData.GenerateCert ();
                //Mobile.configData.LoadCertFromKeychain ();
                
                // Check global min date allowed
                if ( ! string.IsNullOrEmpty ( Global.MinDate ) &&
                     DateTime.Compare ( DateTime.ParseExact ( Global.MinDate, "MM/dd/yyyy", null ), DateTime.Today ) < 0 )
                    throw new DeviceMinDateAllowedException ();
            }
            catch ( Exception e )
            {
                if ( ! avoidXmlError )
                {
                    if (Errors.IsOwnException(e))
                        throw e;
                    else if (e is FileNotFoundException)
                        throw new ConfigurationFilesNotFoundException();
                    else
                    {
                        throw new ConfigurationFilesCorruptedException();
                    }
                }
            }
        }

        public static Configuration GetInstanceWithParams ( string path = "", bool avoidXmlError = false )
        {
            if ( ! Singleton.Has<Configuration> () )
                Singleton.Set = new Configuration ( path, avoidXmlError );

            return Singleton.Get.Configuration;
        }

        public static bool CheckLoadXML()
        {
            string configPath = Mobile.ConfigPath;
            try
            {
                // Load configuration files ( xml's )
                MtuTypes   auxMtus   = Utils.DeserializeXml<MtuTypes>   ( Path.Combine(configPath, XML_MTUS    ) );
                MeterTypes auxMeters = Utils.DeserializeXml<MeterTypes> ( Path.Combine(configPath, XML_METERS  ) );
                Global     auxGlobal = Utils.DeserializeXml<Global>     ( Path.Combine(configPath, XML_GLOBAL  ) );
                AlarmList  auxAlarm  = Utils.DeserializeXml<AlarmList>  ( Path.Combine(configPath, XML_ALARMS  ) );
                DemandConf auxDemand = Utils.DeserializeXml<DemandConf> ( Path.Combine(configPath, XML_DEMANDS ) );
                User[]     auxUsers  = Utils.DeserializeXml<UserList>   ( Path.Combine(configPath, XML_USERS   ) ).List;
                return true;

            }
            catch (Exception )
            {
                //throw new ConfigurationFilesCorruptedException();
                return false;
            }
        }

        public Mtu[] GetMtuTypes()
        {
            return mtuTypes.Mtus.ToArray();
        }

        public Mtu GetMtuTypeById ( int mtuId )
        {
            Mtu mtu = mtuTypes.FindByMtuId ( mtuId );
            
            // Is not valid MTU ID ( not present in Mtu.xml )
            if ( mtu == null )
                Errors.LogErrorNow ( new MtuMissingException () );
            
            return mtu;
        }

        public Meter[] GetMeterType()
        {
            return meterTypes.Meters.ToArray();
        }

        public MeterTypes GetMeterTypes()
        {
            return meterTypes;
        }

        public Meter getMeterTypeById(int meterId)
        {
            return meterTypes.FindByMterId(meterId);
        }

        public InterfaceParameters[] getAllParamsFromInterface ( Mtu mtu, ActionType actionType )
        {
            return interfaces.GetInterfaceByMtuIdAndAction ( mtu, actionType.ToString () ).getAllParams ();
        }

        public InterfaceParameters[] getLogParamsFromInterface ( Mtu mtu, ActionType actionType )
        {
            return interfaces.GetInterfaceByMtuIdAndAction ( mtu, actionType.ToString () ).getLogParams ();
        }

        public InterfaceParameters[] getUserParamsFromInterface ( Mtu mtu, ActionType actionType )
        {
            return interfaces.GetInterfaceByMtuIdAndAction ( mtu, actionType.ToString () ).getUserParams ();
        }

        public string GetMemoryMapTypeByMtuId ( Mtu mtu )
        {
            return InterfaceAux.GetmemoryMapTypeByMtuId ( mtu );
        }

        public int GetmemoryMapSizeByMtuId ( Mtu mtu )
        {
            return InterfaceAux.GetmemoryMapSizeByMtuId ( mtu );
        }

        public MemRegister getFamilyRegister( Mtu mtu, string regsiter_name)
        {
            try
            {
                return getFamilyRegister ( InterfaceAux.GetmemoryMapTypeByMtuId ( mtu ), regsiter_name);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public MemRegister getFamilyRegister(string family, string regsiter_name)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer ( typeof ( MemRegisterList ) );
                
                using ( TextReader reader = Utils.GetResourceStreamReader ( "family_" + family + ".xml" ) )
                {
                    MemRegisterList list = serializer.Deserialize(reader) as MemRegisterList;
                    if (list.Registers != null)
                    {
                        foreach (MemRegister xmlRegister in list.Registers)
                        {
                            if (xmlRegister.Id.ToLower().Equals(regsiter_name.ToLower()))
                            {
                                return xmlRegister;
                            }
                        }
                    }
                }
            }catch (Exception e) { }

            return null;
        }

        public List<string>  GetVendorsFromMeters()
        {
            return meterTypes.GetVendorsFromMeters(meterTypes.Meters);
        }

        public List<string> GetModelsByVendorFromMeters(String vendor)
        {
            return meterTypes.GetModelsByVendorFromMeters(meterTypes.Meters, vendor);
        }

        public Boolean useDummyDigits()
        {
            return !Global.LiveDigitsOnly;
        }

        public String GetDeviceUUID()
        {
            string return_str = "";

            return_str = deviceUUID;

            /*
            if (device.Equals("PC"))
            {
                return_str = "ACLARATECH-CLE5478L-KGUILER";
            }else
                
            if (device.Equals("Android") || device.Equals("iOS") )
            {
                return_str = deviceUUID;
            }
            */

            return return_str; //get UUID from Xamarin
        }

        public String GetApplicationVersion()
        {

            string return_str = "";

            if (device.Equals("PC"))
            {
                return_str = "2.2.5.0";
            }
            else

            if (device.Equals("Android") || device.Equals("iOS"))
            {
                return_str = version;
            }

            return return_str; //get UUID from Xamarin

        }

        public AlarmList Alarms
        {
            get
            {
                return this.alarms;
            }
        }

        public String getApplicationName()
        {

            string return_str = "";

            if (device.Equals("PC"))
            {
                return_str = "AclaraStarSystemMobileR";
            }
            else

            if (device.Equals("Android") || device.Equals("iOS"))
            {
                return_str = appName;
            }


            return return_str; //get UUID from Xamarin
        }

        public void setPlatform(string device_os)
        {
            
            device = device_os;
        }

        public void setDeviceUUID(string UUID)
        {
            deviceUUID = UUID; 
        }

        public void setVersion(string VERSION)
        {
            version = VERSION;
        }

        public void setAppName(string NAME)
        {
            appName = NAME;
        }
    }
}