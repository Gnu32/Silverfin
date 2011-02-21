/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
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
using Nini.Config;
using Aurora.Simulation.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Simulation
{
    public class SimulationServiceInConnector : ISharedRegionModule
    {
        private ISimulationService m_LocalSimulationService;

        public string Name
        {
            get { return GetType().Name; }
        }

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
        }

        public void PostInitialise()
        {
        }

        public void AddRegion(OpenSim.Region.Framework.Scenes.Scene scene)
        {
            IConfig handlerConfig = scene.Config.Configs["Handlers"];
            if (handlerConfig.GetString("SimulationInHandler", "") != Name)
                return;

            if (m_LocalSimulationService != null)
                return;

            bool secure = handlerConfig.GetBoolean("SecureSimulation", true);
            IHttpServer server = scene.RequestModuleInterface<ISimulationBase>().GetHttpServer((uint)handlerConfig.GetInt("SimulationInHandlerPort"));

            m_LocalSimulationService = scene.RequestModuleInterface<ISimulationService>();

            string path = "/" + UUID.Random().ToString() + "/agent/";

            IGridRegisterModule registerModule = scene.RequestModuleInterface<IGridRegisterModule>();
            if (registerModule != null && secure)
                registerModule.AddGenericInfo("SimulationAgent", path);
            else
            {
                secure = false;
                path = "/agent/";
            }

            server.AddHTTPHandler(path, new AgentHandler(m_LocalSimulationService.GetInnerService(), secure).Handler);
            server.AddHTTPHandler("/object/", new ObjectHandler(m_LocalSimulationService.GetInnerService()).Handler);
        }

        public void RegionLoaded(OpenSim.Region.Framework.Scenes.Scene scene)
        {
        }

        public void RemoveRegion(OpenSim.Region.Framework.Scenes.Scene scene)
        {
        }

        public void Close()
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion
    }
}