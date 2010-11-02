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
using System.IO;

namespace OpenSim.Framework
{
    public class VersionInfo
    {
        private const string VERSION_NUMBER = "0.1";
        private const Flavour VERSION_FLAVOUR = Flavour.Dev;

        public enum Flavour
        {
            Unknown,
            Dev,
            RC1,
            RC2,
            Release,
            Post_Fixes
        }

        public static string Version
        {
            get { return GetVersionString(VERSION_NUMBER, VERSION_FLAVOUR); }
        }

        public static string GetVersionString(string versionNumber, Flavour flavour)
        {
            string versionString = "Aurora " + versionNumber + " (" + flavour + ")";
            string ReturnValue = versionString.PadRight(VERSIONINFO_VERSION_LENGTH);
            string buildVersion = string.Empty;

            // Add commit hash and date information if available
            // The commit hash and date are stored in a file bin/.version
            // This file can automatically created by a post
            // commit script in the opensim git master repository or
            // by issuing the follwoing command from the top level
            // directory of the opensim repository
            // git log -n 1 --pretty="format:%h: %ci" >bin/.version
            // For the full git commit hash use %H instead of %h
            //
            // The subversion information is deprecated and will be removed at a later date
            // Add subversion revision information if available
            // Try file "svn_revision" in the current directory first, then the .svn info.
            // This allows to make the revision available in simulators not running from the source tree.
            // FIXME: Making an assumption about the directory we're currently in - we do this all over the place
            // elsewhere as well
            string svnRevisionFileName = "svn_revision";
            string svnFileName = ".svn/entries";
            string gitCommitFileName = ".version";
            string inputLine;
            int strcmp;

            if (File.Exists(gitCommitFileName))
            {
                StreamReader CommitFile = File.OpenText(gitCommitFileName);
                buildVersion = CommitFile.ReadLine();
                CommitFile.Close();
                ReturnValue += buildVersion ?? "";
            }

            // Remove the else logic when subversion mirror is no longer used
            else
            {
                if (File.Exists(svnRevisionFileName))
                {
                    StreamReader RevisionFile = File.OpenText(svnRevisionFileName);
                    buildVersion = RevisionFile.ReadLine();
                    buildVersion.Trim();
                    RevisionFile.Close();

                }

                if (string.IsNullOrEmpty(buildVersion) && File.Exists(svnFileName))
                {
                    StreamReader EntriesFile = File.OpenText(svnFileName);
                    inputLine = EntriesFile.ReadLine();
                    while (inputLine != null)
                    {
                        // using the dir svn revision at the top of entries file
                        strcmp = String.Compare(inputLine, "dir");
                        if (strcmp == 0)
                        {
                            buildVersion = EntriesFile.ReadLine();
                            break;
                        }
                        else
                        {
                            inputLine = EntriesFile.ReadLine();
                        }
                    }
                    EntriesFile.Close();
                }

                ReturnValue += string.IsNullOrEmpty(buildVersion) ? "      " : ("." + buildVersion + "     ").Substring(0, 6);
            }
            return ReturnValue;
        }

        public const int VERSIONINFO_VERSION_LENGTH = 27;
        
        /// <value>
        /// This is the external interface version.  It is separate from the OpenSimulator project version.
        /// 
        /// This version number should be 
        /// increased by 1 every time a code change makes the previous OpenSimulator revision incompatible
        /// with the new revision.  This will usually be due to interregion or grid facing interface changes.
        /// 
        /// Changes which are compatible with an older revision (e.g. older revisions experience degraded functionality
        /// but not outright failure) do not need a version number increment.
        /// 
        /// Having this version number allows the grid service to reject connections from regions running a version
        /// of the code that is too old. 
        ///
        /// </value>
        public readonly static int MajorInterfaceVersion = 6;
    }
}