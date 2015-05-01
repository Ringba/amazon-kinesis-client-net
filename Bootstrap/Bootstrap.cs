﻿/*
 * Copyright 2015 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 *
 * Licensed under the Amazon Software License (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 *
 *  http://aws.amazon.com/asl/
 *
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using CommandLine;
using CommandLine.Text;
using System.Linq;
using Microsoft.Win32;

namespace Amazon.Kinesis.ClientLibrary.Bootstrap
{
    /// <summary>
    /// Represents a Maven java package. We need to download a bunch of these in order
    /// to use the java KCL.
    /// </summary>
    internal class MavenPackage
    {
        public readonly String GroupId;
        public readonly String ArtifactId;
        public readonly String Version;

        /// <summary>
        /// Gets the name of the jar file of this Maven package.
        /// </summary>
        /// <value>The name of the jar file.</value>
        public String FileName
        {
            get
            {
                return String.Format("{0}-{1}.jar", ArtifactId, Version);
            }
        }

        public MavenPackage(String groupId, String artifactId, String version)
        {
            GroupId = groupId;
            ArtifactId = artifactId;
            Version = version;
        }

        /// <summary>
        /// Check if the jar file for this Maven package already exists on disk.
        /// </summary>
        /// <param name="folder">Folder to look in.</param>
        public bool Exists(String folder)
        {
            return File.Exists(Path.Combine(folder, FileName));
        }

        /// <summary>
        /// Download the jar file for this Maven package.
        /// </summary>
        /// <param name="folder">Folder to download the file into.</param>
        public void Fetch(String folder)
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            String destination = Path.Combine(folder, FileName);
            if (!File.Exists(destination))
            {
                var client = new System.Net.WebClient();
                Console.Error.WriteLine(Url + " --> " + destination);
                client.DownloadFile(new Uri(Url), destination);
            }
        }

        /// <summary>
        /// Gets the URL to the jar file for this Maven package.
        /// </summary>
        /// <value>The URL.</value>
        private String Url
        {
            get
            {
                List<String> urlParts = new List<String>();
                urlParts.AddRange(GroupId.Split('.'));
                urlParts.Add(ArtifactId);
                urlParts.Add(Version);
                urlParts.Add(FileName);
                return "http://search.maven.org/remotecontent?filepath=" + String.Join("/", urlParts);
            }
        }
    }

    /// <summary>
    /// Command line options.
    /// </summary>
    class Options
    {
        [Option('j', "java", Required = false, HelpText = "Path to java, used to start the KCL multi-lang daemon. Attempts to auto-detect if not specified.")]
        public string JavaLocation { get; set; }

        [Option('p', "properties", Required = true, HelpText = "Path to properties file used to configure the KCL.")]
        public string PropertiesFile { get; set; }

        [Option("jar-folder", Required = false, HelpText = "Folder to place required jars in. Defaults to ./jars")]
        public string JarFolder { get; set; }

        [Option('e', "execute", HelpText = "Actually launch the KCL. If not specified, prints the command used to launch the KCL.")]
        public bool ShouldExecute { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var help = new HelpText
            {
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true
            };
            help.AddOptions(this);
            return help;
        }
    }

    internal enum OperatingSystemCategory
    {
        UNIX,
        WINDOWS
    }

    /// <summary>
    /// The Bootstrap program helps the user download and launch the KCL multi-lang daemon (which is in java).
    /// </summary>
    class MainClass
    {
        private static readonly OperatingSystemCategory CURRENT_OS = Environment.OSVersion.ToString().Contains("Unix")
            ? OperatingSystemCategory.UNIX
            : OperatingSystemCategory.WINDOWS;

        private static readonly List<MavenPackage> MAVEN_PACKAGES = new List<MavenPackage>()
        {
            new MavenPackage("com.amazonaws", "amazon-kinesis-client", "1.2.1"),
            new MavenPackage("com.fasterxml.jackson.core", "jackson-core", "2.1.1"),
            new MavenPackage("org.apache.httpcomponents", "httpclient", "4.2"),
            new MavenPackage("org.apache.httpcomponents", "httpcore", "4.2"),
            new MavenPackage("com.fasterxml.jackson.core", "jackson-annotations", "2.1.1"),
            new MavenPackage("commons-codec", "commons-codec", "1.3"),
            new MavenPackage("joda-time", "joda-time", "2.4"),
            new MavenPackage("com.amazonaws", "aws-java-sdk", "1.8.11"),
            new MavenPackage("com.amazonaws", "aws-java-sdk-core", "1.8.11"),
            new MavenPackage("com.fasterxml.jackson.core", "jackson-databind", "2.1.1"),
            new MavenPackage("commons-logging", "commons-logging", "1.1.1"),
        };

        /// <summary>
        /// Downloads all the required jars from Maven and returns a classpath string that includes all those jars.
        /// </summary>
        /// <returns>Classpath string that includes all the jars downloaded.</returns>
        /// <param name="jarFolder">Folder into which to save the jars.</param>
        private static string FetchJars(string jarFolder)
        {
            if (jarFolder == null)
            {
                jarFolder = "jars";
            }
            if (!Path.IsPathRooted(jarFolder))
            {
                jarFolder = Path.Combine(Directory.GetCurrentDirectory(), jarFolder);
            }

            Console.Error.WriteLine("Fetching required jars...");

            foreach (MavenPackage mp in MAVEN_PACKAGES)
            {
                mp.Fetch(jarFolder);
            }
            Console.Error.WriteLine("Done.");

            List<string> files = Directory.GetFiles(jarFolder).Where(f => f.EndsWith(".jar")).ToList();
            files.Add(Directory.GetCurrentDirectory());
            return string.Join(Path.PathSeparator.ToString(), files);
        }

        private static string FindJava(string java)
        {
            // See if "java" is already in path and working.
            if (java == null)
            {
                java = "java";
            }
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = java,
                    Arguments = "-version",
                    UseShellExecute = false
                }
            };
            try
            {
                proc.Start();
                proc.WaitForExit();
                return java;
            }
            catch
            {
            }

            // Failing that, look in the registry.
            foreach (var view in new [] { RegistryView.Registry64, RegistryView.Registry32 })
            { 
                var localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                var javaRootKey = localKey.OpenSubKey(@"SOFTWARE\JavaSoft\Java Runtime Environment");
                foreach (var jreKeyName in javaRootKey.GetSubKeyNames())
                {
                    var jreKey = javaRootKey.OpenSubKey(jreKeyName);
                    var javaHome = jreKey.GetValue("JavaHome") as string;
                    var javaExe = Path.Combine(javaHome, "bin", "java.exe");
                    if (File.Exists(javaExe))
                    {
                        return javaExe;
                    }
                }
            }
                
            return null;
        }

        public static void Main(string[] args)
        {
            var options = new Options();
            if (Parser.Default.ParseArguments(args, options))
            {
                string javaClassPath = FetchJars(options.JarFolder);
                string java = FindJava(options.JavaLocation);

                if (java == null)
                {
                    Console.Error.WriteLine("java could not be found. You may need to install it, or manually specifiy the path to it.");
                    Environment.Exit(2);
                }

                List<string> cmd = new List<string>()
                {
                    java,
                    "-cp",
                    javaClassPath,
                    "com.amazonaws.services.kinesis.multilang.MultiLangDaemon",
                    options.PropertiesFile
                };
                if (options.ShouldExecute)
                {
                    // Start the KCL.
                    Process proc = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = cmd[0],
                            Arguments = string.Join(" ", cmd.Skip(1)),
                            UseShellExecute = false
                        }
                    };
                    proc.Start();
                    proc.WaitForExit();
                }
                else
                {
                    // Print out a command that can be used to start the KCL.
                    string c = string.Join(" ", cmd.Select(f => "\"" + f + "\""));
                    if (CURRENT_OS == OperatingSystemCategory.WINDOWS)
                    {
                        c = "& " + c;
                    }
                    Console.WriteLine(c);
                }
            }
        }
    }
}
