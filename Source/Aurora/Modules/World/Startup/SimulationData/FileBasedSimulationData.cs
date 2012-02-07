/*
 * Copyright (c) Contributors, http://aurora-sim.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Aurora-Sim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Timers;
using System.Windows.Forms;
using Aurora.Framework;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Aurora.Framework.Serialization;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using Timer = System.Timers.Timer;

namespace Aurora.Modules.Startup.FileBasedSimulationData
{
    /// <summary>
    ///   FileBased DataStore, do not store anything in any databases, instead save .abackup files for it
    /// </summary>
    public class FileBasedSimulationData : ISimulationDataStore
    {
        protected Timer m_backupSaveTimer;

        protected string m_fileName = "";
        protected List<ISceneEntity> m_groups = new List<ISceneEntity>();
        protected bool m_keepOldSave = true;
        protected string m_loadAppenedFileName = "";
        protected string m_loadDirectory = "";
        protected bool m_loaded;
        protected string m_oldSaveDirectory = "Backups";
        protected bool m_oldSaveHasBeenSaved;
        protected byte[] m_oldstylerevertTerrain;
        protected byte[] m_oldstyleterrain;
        //For backwards compat
        //For backwards compat
        protected List<LandData> m_parcels = new List<LandData>();
        protected bool m_requiresSave = true;
        protected bool m_displayNotSavingNotice = true;
        protected byte[] m_revertTerrain;
        protected byte[] m_revertWater;
        protected string m_saveAppenedFileName = "";
        protected bool m_saveBackupChanges = true;
        protected bool m_saveBackups;
        protected bool m_saveChanges = true;
        protected string m_saveDirectory = "";
        protected Timer m_saveTimer;
        protected IScene m_scene;
        protected short[] m_shortrevertTerrain;
        protected short[] m_shortterrain;
        protected byte[] m_terrain;
        protected int m_timeBetweenBackupSaves = 1440; //One day
        protected int m_timeBetweenSaves = 5;
        protected byte[] m_water;

        #region ISimulationDataStore Members

        public bool MapTileNeedsGenerated
        {
            get;
            set;
        }

        public virtual string Name
        {
            get { return "FileBasedDatabase"; }
        }

        public bool SaveBackups
        {
            get { return m_saveBackups; }
            set { m_saveBackups = value; }
        }

        public virtual ISimulationDataStore Copy()
        {
            return new FileBasedSimulationData();
        }

        public virtual void Initialise()
        {
        }

        public virtual List<ISceneEntity> LoadObjects(IScene scene)
        {
            if (!m_loaded)
            {
                m_loaded = true;
                ReadConfig(scene, scene.Config.Configs["FileBasedSimulationData"]);
                ReadBackup(scene);
            }
            return m_groups;
        }

        public virtual short[] LoadTerrain(IScene scene, bool RevertMap, int RegionSizeX, int RegionSizeY)
        {
            if (!m_loaded)
            {
                m_loaded = true;
                ReadConfig(scene, scene.Config.Configs["FileBasedSimulationData"]);
                ReadBackup(scene);
            }
            ITerrainModule terrainModule = scene.RequestModuleInterface<ITerrainModule>();
            if (RevertMap)
            {
                ITerrainChannel channel = new TerrainChannel(false, scene);
                if (m_revertTerrain == null)
                {
                    if (m_shortrevertTerrain != null) //OpenSim style
                        terrainModule.TerrainRevertMap = new TerrainChannel(m_shortrevertTerrain, scene);
                    else if (m_oldstylerevertTerrain != null)
                    {
                        MemoryStream ms = new MemoryStream(m_oldstylerevertTerrain);
                        if (terrainModule != null)
                            terrainModule.LoadRevertMapFromStream(".r32", ms, 0, 0);
                    }
                }
                else
                    //New style
                    terrainModule.TerrainRevertMap = ReadFromData(m_revertTerrain, scene);
                //Make sure the size is right!
                if (terrainModule.TerrainRevertMap != null &&
                    terrainModule.TerrainRevertMap.Height != scene.RegionInfo.RegionSizeX)
                    terrainModule.TerrainRevertMap = null;
                m_revertTerrain = null;
                m_oldstylerevertTerrain = null;
                m_shortrevertTerrain = null;
                return null;
            }
            else
            {
                if (m_terrain == null)
                {
                    if (m_shortterrain != null) //OpenSim style
                        terrainModule.TerrainMap = new TerrainChannel(m_shortterrain, scene);
                    else if (m_oldstyleterrain != null)
                    {
//Old style
                        ITerrainChannel channel = new TerrainChannel(false, scene);
                        MemoryStream ms = new MemoryStream(m_oldstyleterrain);
                        if (terrainModule != null)
                            terrainModule.LoadFromStream(".r32", ms, 0, 0);
                    }
                }
                else
                    //New style
                    terrainModule.TerrainMap = ReadFromData(m_terrain, scene);
                //Make sure the size is right!
                if (terrainModule.TerrainMap != null &&
                    terrainModule.TerrainMap.Height != scene.RegionInfo.RegionSizeX)
                    terrainModule.TerrainMap = null;
                m_terrain = null;
                m_oldstyleterrain = null;
                m_shortterrain = null;
                return null;
            }
        }

        public virtual short[] LoadWater(IScene scene, bool RevertMap, int RegionSizeX, int RegionSizeY)
        {
            if (!m_loaded)
            {
                m_loaded = true;
                ReadConfig(scene, scene.Config.Configs["FileBasedSimulationData"]);
                ReadBackup(scene);
            }
            ITerrainModule terrainModule = scene.RequestModuleInterface<ITerrainModule>();
            if (RevertMap)
            {
                if (m_revertWater == null)
                    return null;
                terrainModule.TerrainWaterRevertMap = ReadFromData(m_revertWater, scene);
                //Make sure the size is right!
                if (terrainModule.TerrainWaterRevertMap.Height != scene.RegionInfo.RegionSizeX)
                    terrainModule.TerrainWaterRevertMap = null;
                m_revertWater = null;
                return null;
            }
            else
            {
                if (m_water == null)
                    return null;
                terrainModule.TerrainWaterMap = ReadFromData(m_water, scene);
                //Make sure the size is right!
                if (terrainModule.TerrainWaterMap.Height != scene.RegionInfo.RegionSizeX)
                    terrainModule.TerrainWaterMap = null;
                m_water = null;
                return null;
            }
        }

        public virtual void Shutdown()
        {
            //The sim is shutting down, we need to save one last backup
            try
            {
                if (!m_saveChanges || !m_saveBackups)
                    return;
                SaveBackup(m_saveDirectory, false);
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Error("[FileBasedSimulationData]: Failed to save backup, exception occured " + ex);
            }
        }

        public virtual void Tainted()
        {
            m_requiresSave = true;
        }

        public virtual void RemoveRegion(UUID regionUUID)
        {
            //Remove the file so that the region is gone
            File.Delete(m_loadDirectory + m_fileName);
        }

        public virtual void RenameBackupFiles(string oldRegionName, string newRegionName, IConfigSource configSource)
        {
            if (File.Exists(m_saveDirectory + oldRegionName + m_saveAppenedFileName + ".abackup"))
                File.Move(m_saveDirectory + oldRegionName + m_saveAppenedFileName + ".abackup",
                          m_saveDirectory + newRegionName + m_saveAppenedFileName + ".abackup");
        }

        /// <summary>
        ///   Around for legacy things
        /// </summary>
        /// <param name = "regionUUID"></param>
        /// <returns></returns>
        public virtual List<LandData> LoadLandObjects(UUID regionUUID)
        {
            if (!m_loaded)
            {
                m_loaded = true;
                ReadConfig(m_scene, m_scene.Config.Configs["FileBasedSimulationData"]);
                ReadBackup(m_scene);
            }
            return m_parcels;
        }

        #endregion

        public object RegionInfoChanged(string funcName, object param)
        {
            RegionInfo oldRegion = (RegionInfo)((object[])param)[0];
            RegionInfo newRegion = (RegionInfo)((object[])param)[1];
            RenameBackupFiles(oldRegion.RegionName, newRegion.RegionName, m_scene.Config);
            return null;
        }

        /// <summary>
        ///   Read the config for the data loader
        /// </summary>
        /// <param name = "scene"></param>
        /// <param name = "config"></param>
        protected virtual void ReadConfig(IScene scene, IConfig config)
        {
            scene.RequestModuleInterface<ISimulationBase>().EventManager.RegisterEventHandler("RegionInfoChanged", RegionInfoChanged);
            if (config != null)
            {
                m_loadAppenedFileName = config.GetString("ApendedLoadFileName", m_loadAppenedFileName);
                m_saveAppenedFileName = config.GetString("ApendedSaveFileName", m_saveAppenedFileName);
                m_saveChanges = config.GetBoolean("SaveChanges", m_saveChanges);
                m_timeBetweenSaves = config.GetInt("TimeBetweenSaves", m_timeBetweenSaves);
                m_keepOldSave = config.GetBoolean("SavePreviousBackup", m_keepOldSave);
                m_oldSaveDirectory = config.GetString("PreviousBackupDirectory", m_oldSaveDirectory);
                m_loadDirectory = config.GetString("LoadBackupDirectory", m_loadDirectory);
                m_saveDirectory = config.GetString("SaveBackupDirectory", m_saveDirectory);
                m_saveBackupChanges = config.GetBoolean("SaveTimedPreviousBackup", m_keepOldSave);
                m_timeBetweenBackupSaves = config.GetInt("TimeBetweenBackupSaves", m_timeBetweenBackupSaves);
            }

            if (!Directory.Exists(m_saveDirectory))
                Directory.CreateDirectory(m_saveDirectory);

            if (m_saveChanges && m_timeBetweenSaves != 0)
            {
                m_saveTimer = new Timer(m_timeBetweenSaves*60*1000);
                m_saveTimer.Elapsed += m_saveTimer_Elapsed;
                m_saveTimer.Start();
            }

            if (m_saveChanges && m_timeBetweenBackupSaves != 0)
            {
                m_backupSaveTimer = new Timer(m_timeBetweenBackupSaves*60*1000);
                m_backupSaveTimer.Elapsed += m_backupSaveTimer_Elapsed;
                m_backupSaveTimer.Start();
            }

            scene.AuroraEventManager.RegisterEventHandler("Backup", AuroraEventManager_OnGenericEvent);

            m_scene = scene;
            m_fileName = scene.RegionInfo.RegionName + m_loadAppenedFileName + ".abackup";
        }

        /// <summary>
        ///   Look for the backup event, and if it is there, trigger the backup of the sim
        /// </summary>
        /// <param name = "FunctionName"></param>
        /// <param name = "parameters"></param>
        /// <returns></returns>
        private object AuroraEventManager_OnGenericEvent(string FunctionName, object parameters)
        {
            if (FunctionName == "Backup")
            {
                m_saveTimer.Stop();
                try
                {
                    SaveBackup(m_saveDirectory, false);
                    m_requiresSave = false;
                }
                catch (Exception ex)
                {
                    MainConsole.Instance.Error("[FileBasedSimulationData]: Failed to save backup, exception occured " + ex);
                }
                m_saveTimer.Start(); //Restart it as we just did a backup
            }
            return null;
        }

        /// <summary>
        ///   Save a backup on the timer event
        /// </summary>
        /// <param name = "sender"></param>
        /// <param name = "e"></param>
        private void m_saveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (m_requiresSave)
            {
                m_displayNotSavingNotice = true;
                m_requiresSave = false;
                m_saveTimer.Stop();
                try
                {
                    if (m_saveChanges && m_saveBackups)
                        SaveBackup(m_saveDirectory, m_keepOldSave && !m_oldSaveHasBeenSaved);
                }
                catch (Exception ex)
                {
                    MainConsole.Instance.Error("[FileBasedSimulationData]: Failed to save backup, exception occured " + ex);
                }
                m_saveTimer.Start(); //Restart it as we just did a backup
            }
            else if (m_displayNotSavingNotice)
            {
                m_displayNotSavingNotice = false;
                MainConsole.Instance.Info("[FileBasedSimulationData]: Not saving backup, not required");
            }
        }

        /// <summary>
        ///   Save a backup into the oldSaveDirectory on the timer event
        /// </summary>
        /// <param name = "sender"></param>
        /// <param name = "e"></param>
        private void m_backupSaveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                SaveBackup(m_oldSaveDirectory, true);
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Error("[FileBasedSimulationData]: Failed to save backup, exception occured " + ex);
            }
        }

        /// <summary>
        ///   Save a backup of the sim
        /// </summary>
        /// <param name = "appendedFilePath">The file path where the backup will be saved</param>
        protected virtual void SaveBackup(string appendedFilePath, bool saveAssets)
        {
            if (appendedFilePath == "/")
                appendedFilePath = "";
            IBackupModule backupModule = m_scene.RequestModuleInterface<IBackupModule>();
            if (backupModule != null && backupModule.LoadingPrims) //Something is changing lots of prims
            {
                MainConsole.Instance.Info("[Backup]: Not saving backup because the backup module is loading prims");
                return;
            }

            //Save any script state saves that might be around
            IScriptModule[] engines = m_scene.RequestModuleInterfaces<IScriptModule>();
            try
            {
                if (engines != null)
                {
#if (!ISWIN)
                    foreach (IScriptModule engine in engines)
                    {
                        if (engine != null)
                        {
                            engine.SaveStateSaves();
                        }
                    }
#else
                    foreach (IScriptModule engine in engines.Where(engine => engine != null))
                    {
                        engine.SaveStateSaves();
                    }
#endif
                }
            }
            catch (Exception ex)
            {
                MainConsole.Instance.WarnFormat("[Backup]: Exception caught: {0}", ex);
            }

            MainConsole.Instance.Info("[FileBasedSimulationData]: Saving Backup for region " + m_scene.RegionInfo.RegionName);
            string fileName = Path.Combine(appendedFilePath, m_scene.RegionInfo.RegionName) + m_saveAppenedFileName + ".abackup";
            if (File.Exists(fileName))
            {
                //Do new style saving here!
                GZipStream m_saveStream = new GZipStream(new FileStream(fileName + ".tmp", FileMode.Create),
                                                         CompressionMode.Compress);
                TarArchiveWriter writer = new TarArchiveWriter(m_saveStream);
                GZipStream m_loadStream = new GZipStream(new FileStream(fileName, FileMode.Open),
                                                         CompressionMode.Decompress);
                TarArchiveReader reader = new TarArchiveReader(m_loadStream);

                writer.WriteDir("parcels");

                IParcelManagementModule module = m_scene.RequestModuleInterface<IParcelManagementModule>();
                if (module != null)
                {
                    List<ILandObject> landObject = module.AllParcels();
                    foreach (ILandObject parcel in landObject)
                    {
                        OSDMap parcelMap = parcel.LandData.ToOSD();
                        writer.WriteFile("parcels/" + parcel.LandData.GlobalID.ToString(),
                                         OSDParser.SerializeLLSDBinary(parcelMap));
                        parcelMap = null;
                    }
                }

                writer.WriteDir("newstyleterrain");
                writer.WriteDir("newstylerevertterrain");

                writer.WriteDir("newstylewater");
                writer.WriteDir("newstylerevertwater");

                ITerrainModule tModule = m_scene.RequestModuleInterface<ITerrainModule>();
                if (tModule != null)
                {
                    try
                    {
                        byte[] sdata = WriteTerrainToStream(tModule.TerrainMap);
                        writer.WriteFile("newstyleterrain/" + m_scene.RegionInfo.RegionID.ToString() + ".terrain", sdata);
                        sdata = null;

                        sdata = WriteTerrainToStream(tModule.TerrainRevertMap);
                        writer.WriteFile(
                            "newstylerevertterrain/" + m_scene.RegionInfo.RegionID.ToString() + ".terrain", sdata);
                        sdata = null;

                        if (tModule.TerrainWaterMap != null)
                        {
                            sdata = WriteTerrainToStream(tModule.TerrainWaterMap);
                            writer.WriteFile("newstylewater/" + m_scene.RegionInfo.RegionID.ToString() + ".terrain",
                                             sdata);
                            sdata = null;

                            sdata = WriteTerrainToStream(tModule.TerrainWaterRevertMap);
                            writer.WriteFile(
                                "newstylerevertwater/" + m_scene.RegionInfo.RegionID.ToString() + ".terrain", sdata);
                            sdata = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        MainConsole.Instance.WarnFormat("[Backup]: Exception caught: {0}", ex);
                    }
                }

                IDictionary<UUID, AssetType> assets = new Dictionary<UUID, AssetType>();
                UuidGatherer assetGatherer = new UuidGatherer(m_scene.AssetService);

                ISceneEntity[] saveentities = m_scene.Entities.GetEntities();
                List<UUID> entitiesToSave = new List<UUID>();
                foreach (ISceneEntity entity in saveentities)
                {
                    try
                    {
                        if (entity.IsAttachment ||
                            ((entity.RootChild.Flags & PrimFlags.Temporary) == PrimFlags.Temporary)
                            || ((entity.RootChild.Flags & PrimFlags.TemporaryOnRez) == PrimFlags.TemporaryOnRez))
                            continue;
                        if (entity.HasGroupChanged)
                        {
                            entity.HasGroupChanged = false;
                            //Write all entities
                            byte[] xml = ((ISceneObject) entity).ToBinaryXml2();
                            writer.WriteFile("entities/" + entity.UUID.ToString(), xml);
                            xml = null;
                        }
                        else
                            entitiesToSave.Add(entity.UUID);
                        if (saveAssets)
                            assetGatherer.GatherAssetUuids(entity, assets, m_scene);
                    }
                    catch (Exception ex)
                    {
                        MainConsole.Instance.WarnFormat("[Backup]: Exception caught: {0}", ex);
                        entitiesToSave.Add(entity.UUID);
                    }
                }


                byte[] data;
                string filePath;
                TarArchiveReader.TarEntryType entryType;
                //Load the archive data that we need
                try
                {
                    while ((data = reader.ReadEntry(out filePath, out entryType)) != null)
                    {
                        if (TarArchiveReader.TarEntryType.TYPE_DIRECTORY == entryType)
                            continue;
                        if (filePath.StartsWith("entities/"))
                        {
                            UUID entityID = UUID.Parse(filePath.Remove(0, 9));
                            if (entitiesToSave.Contains(entityID))
                            {
                                writer.WriteFile(filePath, data);
                                entitiesToSave.Remove(entityID);
                            }
                        }
                        data = null;
                    }
                }
                catch (Exception ex)
                {
                    MainConsole.Instance.WarnFormat("[Backup]: Exception caught: {0}", ex);
                }

                if (entitiesToSave.Count > 0)
                {
                    MainConsole.Instance.Fatal(entitiesToSave.Count + " PRIMS WERE NOT GOING TO BE SAVED! FORCE SAVING NOW! ");
                    foreach (ISceneEntity entity in saveentities)
                    {
                        if (entitiesToSave.Contains(entity.UUID))
                        {
                            if (entity.IsAttachment ||
                                ((entity.RootChild.Flags & PrimFlags.Temporary) == PrimFlags.Temporary)
                                || ((entity.RootChild.Flags & PrimFlags.TemporaryOnRez) == PrimFlags.TemporaryOnRez))
                                continue;
                            //Write all entities
                            byte[] xml = ((ISceneObject) entity).ToBinaryXml2();
                            writer.WriteFile("entities/" + entity.UUID.ToString(), xml);
                            xml = null;
                        }
                    }
                }

                if (saveAssets)
                {
                    foreach (UUID assetID in new List<UUID>(assets.Keys))
                    {
                        try
                        {
                            WriteAsset(assetID.ToString(), m_scene.AssetService.Get(assetID.ToString()), writer);
                        }
                        catch (Exception ex)
                        {
                            MainConsole.Instance.WarnFormat("[Backup]: Exception caught: {0}", ex);
                        }
                    }
                }

                writer.Close();
                m_loadStream.Close();
                m_saveStream.Close();
                GC.Collect();

                if (m_keepOldSave && !m_oldSaveHasBeenSaved)
                {
                    //Havn't moved it yet, so make sure the directory exists, then move it
                    m_oldSaveHasBeenSaved = true;
                    if (!Directory.Exists(m_oldSaveDirectory))
                        Directory.CreateDirectory(m_oldSaveDirectory);
                    File.Copy(fileName + ".tmp",
                              Path.Combine(m_oldSaveDirectory,
                                           m_scene.RegionInfo.RegionName + SerializeDateTime() + m_saveAppenedFileName +
                                           ".abackup"));
                }
                //Just remove the file
                File.Delete(fileName);
            }
            else
            {
                //Add the .temp since we might need to make a backup and so that if something goes wrong, we don't corrupt the main backup
                GZipStream m_saveStream = new GZipStream(new FileStream(fileName + ".tmp", FileMode.Create),
                                                         CompressionMode.Compress);
                TarArchiveWriter writer = new TarArchiveWriter(m_saveStream);
                IAuroraBackupArchiver archiver = m_scene.RequestModuleInterface<IAuroraBackupArchiver>();

                //Turn off prompting so that we don't ask the user questions every time we need to save the backup
                archiver.AllowPrompting = false;
                archiver.SaveRegionBackup(writer, m_scene);
                archiver.AllowPrompting = true;

                //If we got this far, we assume that everything went well, so now we move the stuff around
                if (File.Exists(fileName))
                {
                    //If keepOldSave is enabled, the user wants us to move the first backup that we originally loaded from into the oldSaveDirectory
                    if (m_keepOldSave && !m_oldSaveHasBeenSaved)
                    {
                        //Havn't moved it yet, so make sure the directory exists, then move it
                        m_oldSaveHasBeenSaved = true;
                        if (!Directory.Exists(m_oldSaveDirectory))
                            Directory.CreateDirectory(m_oldSaveDirectory);
                        File.Copy(fileName + ".tmp",
                                  Path.Combine(m_oldSaveDirectory,
                                               m_scene.RegionInfo.RegionName + SerializeDateTime() +
                                               m_saveAppenedFileName + ".abackup"));
                    }
                    //Just remove the file
                    File.Delete(fileName);
                }
            }
            ISceneEntity[] entities = m_scene.Entities.GetEntities();
            try
            { 
#if (!ISWIN)
                foreach (ISceneEntity entity in entities)
                {
                    if (entity.HasGroupChanged)
                    {
                        entity.HasGroupChanged = false;
                    }
                }
#else
                foreach (ISceneEntity entity in entities.Where(entity => entity.HasGroupChanged))
                {
                    entity.HasGroupChanged = false;
                }
#endif
            }
            catch (Exception ex)
            {
                MainConsole.Instance.WarnFormat("[Backup]: Exception caught: {0}", ex);
            }
            //Now make it the full file again
            File.Move(fileName + ".tmp", fileName);
            MapTileNeedsGenerated = true;
            MainConsole.Instance.Info("[FileBasedSimulationData]: Saved Backup for region " + m_scene.RegionInfo.RegionName);
        }

        private void WriteAsset(string id, AssetBase asset, TarArchiveWriter writer)
        {
            if (asset != null)
                writer.WriteFile("assets/" + asset.ID, OSDParser.SerializeJsonString(asset.ToOSD()));
            else
                MainConsole.Instance.WarnFormat("[FileBasedSimulationData]: Could not find asset {0} to save.", id);
        }

        private byte[] WriteTerrainToStream(ITerrainChannel tModule)
        {
            int tMapSize = tModule.Height*tModule.Height;
            byte[] sdata = new byte[tMapSize*2];
            Buffer.BlockCopy(tModule.GetSerialised(tModule.Scene), 0, sdata, 0, sdata.Length);
            return sdata;
        }

        protected virtual string SerializeDateTime()
        {
            return String.Format("--{0:yyyy-MM-dd-HH-mm}", DateTime.Now);
            //return "--" + DateTime.Now.Month + "-" + DateTime.Now.Day + "-" + DateTime.Now.Hour + "-" + DateTime.Now.Minute;
        }

        protected virtual void ReadBackup(IScene scene)
        {
            MainConsole.Instance.Info("[FileBasedSimulationData]: Reading file for " + scene.RegionInfo.RegionName);
            List<uint> foundLocalIDs = new List<uint>();
            GZipStream m_loadStream;
            try
            {
                m_loadStream =
                    new GZipStream(
                        ArchiveHelpers.GetStream(((m_loadDirectory == "" || m_loadDirectory == "/")
                                                      ? m_fileName
                                                      : Path.Combine(m_loadDirectory, m_fileName))),
                        CompressionMode.Decompress);
            }
            catch
            {
                if (CheckForOldDataBase())
                    SaveBackup(m_saveDirectory, false);
                return;
            }
            TarArchiveReader reader = new TarArchiveReader(m_loadStream);

            byte[] data;
            string filePath;
            TarArchiveReader.TarEntryType entryType;
            //Load the archive data that we need
            while ((data = reader.ReadEntry(out filePath, out entryType)) != null)
            {
                if (TarArchiveReader.TarEntryType.TYPE_DIRECTORY == entryType)
                    continue;

                if (filePath.StartsWith("parcels/"))
                {
                    //Only use if we are not merging
                    LandData parcel = new LandData();
                    OSD parcelData = OSDParser.DeserializeLLSDBinary(data);
                    parcel.FromOSD((OSDMap) parcelData);
                    m_parcels.Add(parcel);
                }
                else if (filePath.StartsWith("terrain/"))
                {
                    m_oldstyleterrain = data;
                }
                else if (filePath.StartsWith("revertterrain/"))
                {
                    m_oldstylerevertTerrain = data;
                }
                else if (filePath.StartsWith("newstyleterrain/"))
                {
                    m_terrain = data;
                }
                else if (filePath.StartsWith("newstylerevertterrain/"))
                {
                    m_revertTerrain = data;
                }
                else if (filePath.StartsWith("newstylewater/"))
                {
                    m_water = data;
                }
                else if (filePath.StartsWith("newstylerevertwater/"))
                {
                    m_revertWater = data;
                }
                else if (filePath.StartsWith("entities/"))
                {
                    MemoryStream ms = new MemoryStream(data);
                    SceneObjectGroup sceneObject = SceneObjectSerializer.FromXml2Format(ref ms, scene);
                    ms.Close();
                    ms = null;
                    data = null;
                    foreach (ISceneChildEntity part in sceneObject.ChildrenEntities())
                    {
                        if (!foundLocalIDs.Contains(part.LocalId))
                            foundLocalIDs.Add(part.LocalId);
                        else
                            part.LocalId = 0; //Reset it! Only use it once!
                    }
                    m_groups.Add(sceneObject);
                }
                data = null;
            }
            m_loadStream.Close();
            m_loadStream = null;
            foundLocalIDs.Clear();
            GC.Collect();
        }

        /// <summary>
        ///   Checks whether an older style database exists
        /// </summary>
        /// <returns>Whether an older style database exists</returns>
        protected virtual bool CheckForOldDataBase()
        {
            string connString = "";
            string name = "";
            // Try reading the [DatabaseService] section, if it exists
            IConfig dbConfig = m_scene.Config.Configs["DatabaseService"];
            if (dbConfig != null)
                connString = dbConfig.GetString("ConnectionString", String.Empty);

            // Try reading the [SimulationDataStore] section
            IConfig simConfig = m_scene.Config.Configs["SimulationDataStore"];
            if (simConfig != null)
            {
                name = simConfig.GetString("LegacyDatabaseLoaderName", "FileBasedDatabase");
                connString = simConfig.GetString("ConnectionString", connString);
            }

            ILegacySimulationDataStore[] stores =
                AuroraModuleLoader.PickupModules<ILegacySimulationDataStore>().ToArray();
#if (!ISWIN)
            ILegacySimulationDataStore simStore = null;
            foreach (ILegacySimulationDataStore store in stores)
            {
                if (store.Name == name)
                {
                    simStore = store;
                    break;
                }
            }
#else
            ILegacySimulationDataStore simStore = stores.FirstOrDefault(store => store.Name == name);
#endif
            if (simStore == null)
                return false;

            simStore.Initialise(connString);

            IParcelServiceConnector conn = DataManager.DataManager.RequestPlugin<IParcelServiceConnector>();
            m_parcels = simStore.LoadLandObjects(m_scene.RegionInfo.RegionID);
            m_parcels.AddRange(conn.LoadLandObjects(m_scene.RegionInfo.RegionID));
            m_groups = simStore.LoadObjects(m_scene.RegionInfo.RegionID, m_scene);
            if (m_groups.Count != 0 || m_parcels.Count != 0)
            {
                try
                {
                    m_shortterrain = simStore.LoadTerrain(m_scene, false, m_scene.RegionInfo.RegionSizeX,
                                                          m_scene.RegionInfo.RegionSizeY);
                    m_shortrevertTerrain = simStore.LoadTerrain(m_scene, true, m_scene.RegionInfo.RegionSizeX,
                                                                m_scene.RegionInfo.RegionSizeY);
                    //Remove these so that we don't get stuck loading them later
                    conn.RemoveAllLandObjects(m_scene.RegionInfo.RegionID);
                    simStore.RemoveAllLandObjects(m_scene.RegionInfo.RegionID);
                }
                catch
                { }
                return true;
            }
            return false;
        }

        private ITerrainChannel ReadFromData(byte[] data, IScene scene)
        {
            short[] sdata = new short[data.Length/2];
            Buffer.BlockCopy(data, 0, sdata, 0, data.Length);
            return new TerrainChannel(sdata, scene);
        }
    }
}