using System;
using System.IO;
using System.Xml;
using System.Windows;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SimpleSekiroSavegameHelper
{
    [DataContract(Name = "SimpleSekiroSavegameHelper", Namespace = "")]
    public class ApplicationSettings
    {
        /**
         * Settings definition
         */
        [DataMember]
        public Dictionary<string, string> names { get; set; }

        public ApplicationSettings()
        {
            names = new Dictionary<string, string>();
        }
    }

    public class SettingsService
    {
        private readonly string _sConfigurationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + @"\config.xml";

        /// <summary>
        /// Read and store settings here.
        /// </summary>
        public ApplicationSettings ApplicationSettings;

        /// <summary>
        /// Create a settings provider to load and save settings.
        /// </summary>
        /// <param name="settingsFilePath">The file path to the settings file.</param>
        public SettingsService(string settingsFilePath = null)
        {
            if (settingsFilePath != null) _sConfigurationPath = settingsFilePath;
            ApplicationSettings = new ApplicationSettings();
        }

        /// <summary>
        /// Load settings from file into settings property.
        /// </summary>
        /// <returns></returns>
        internal bool Load()
        {
            if (!File.Exists(_sConfigurationPath)) return false;

            DataContractSerializer xmlSerializer = new DataContractSerializer(typeof(ApplicationSettings));
            using (FileStream streamReader = new FileStream(_sConfigurationPath, FileMode.Open))
            {
                try
                {
                    ApplicationSettings = (ApplicationSettings)xmlSerializer.ReadObject(streamReader);
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error while loading configuration file:\n" + ex.Message, "Simple Sekiro Savegame Helper");
                }
            }
            return false;
        }

        /// <summary>
        /// Save settings from settings property to file.
        /// </summary>
        internal void Save()
        {
            DataContractSerializer xmlSerializer = new DataContractSerializer(typeof(ApplicationSettings), new DataContractSerializerSettings {  });
            using (XmlWriter xmlWriter = XmlWriter.Create(_sConfigurationPath, new XmlWriterSettings { Indent = true, IndentChars = "\t" }))
            {
                try
                {
                    xmlSerializer.WriteObject(xmlWriter, ApplicationSettings);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error while writing configuration file:\n" + ex.Message, "Simple Sekiro Savegame Helper");
                }
            }
        }

        /// <summary>
        /// Clears all settings and deletes the settings file.
        /// </summary>
        internal void Clear()
        {
            ApplicationSettings = new ApplicationSettings();
            try
            {
                if (File.Exists(_sConfigurationPath))
                    File.Delete(_sConfigurationPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while trying to delete configuration file:\n" + ex.Message, "Simple Sekiro Savegame Helper");
            }
        }
    }
}
