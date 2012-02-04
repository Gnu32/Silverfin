/*
 * Copyright (c) Contributors, http://aurora-sim.org/, http://opensimulator.org/
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

namespace Aurora.Framework
{
    public class Constants
    {
        public const int RegionSize = 256;
        public const byte TerrainPatchSize = 16;
        public const float TerrainCompression = 100.0f;
        public const int MinRegionSize = 16;

        public const string PathModules = "Libraries";
        public const string PathResources = "Resources";
        public const string PathWWW = PathResources + "/WWW";
        public const string PathConfigGrid = "Config";
        public const string PathConfigServer = "Config.Server";
        public const string PathCaches = "Caches";

        public const string ConsoleSeperator = "===================:>";
        public const string ConsoleLogo = @"
       o 8                      ooooo  o       
         8                      8              
.oPYo. o8 8 o    o .oPYo. oPYo. o8oo   o8 odYo. 
Yb..    8 8 Y.  .P 8oooo8 8  `'  8      8 8' `8 
  'Yb.  8 8 `b..d' 8.     8      8      8 8   8 
`YooP'  8 8  `YP'  `Yooo' 8      8      8 8   8 
:.....::....::...:::.....:..:::::..:::::....::..
:::::::::::::::::::: Based on Aurora & OpenSim :";
    }
}