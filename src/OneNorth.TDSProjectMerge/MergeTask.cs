using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OneNorth.TDSProjectMerge
{
    public class MergeTask
    {
        public void MergeProjects(string targetProject, string contentProject)
        {
            //Validate the parameters
            if (targetProject == null)
                throw new ArgumentNullException("targetProject");

            if (contentProject == null)
                throw new ArgumentNullException("contentProject");

            if (!File.Exists(targetProject))
                throw new FileNotFoundException(targetProject);

            if (!File.Exists(contentProject))
                throw new FileNotFoundException(contentProject);

            XNamespace buildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

            //Load the projects
            var targetProjectXml = XDocument.Load(targetProject);
            var contentProjectXml = XDocument.Load(contentProject);

            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("ms", buildNamespace.NamespaceName);

            //Find the Element that holds all the SitecoreItem elements
            var targetItemGroup = FindTargetItemGroup(buildNamespace, targetProjectXml, ns, "SitecoreItem");

            //Move the list of elements into a dictionary
            MoveBuildItems(buildNamespace, contentProjectXml, ns, targetItemGroup, "SitecoreItem");

            targetItemGroup = FindTargetItemGroup(buildNamespace, targetProjectXml, ns, "CodeGenTemplate");

            MoveBuildItems(buildNamespace, contentProjectXml, ns, targetItemGroup, "CodeGenTemplate");

            //Find the code gen properties
            var targetPropertyGroup = targetProjectXml.XPathSelectElement("/ms:Project/ms:PropertyGroup[1]", ns);
            var sourceElements = contentProjectXml.XPathSelectElements("//ms:EnableCodeGeneration|//ms:FieldsForCodeGen|//ms:CodeGenFile|//ms:CodeGenTargetProject|//ms:BaseTransformFile|//ms:HeaderTransformFile|//ms:BaseNamespace", ns).ToList();

            //Disconnect from the existing xml
            sourceElements.ForEach(e => e.Remove());

            //add to the target
            sourceElements.ForEach(e => targetPropertyGroup.Elements(e.Name).ToList().ForEach(i => i.Remove()));
            targetPropertyGroup.Add(sourceElements.ToArray());

            //Save the project
            targetProjectXml.Save(targetProject);
        }

        private static void MoveBuildItems(XNamespace buildNamespace, XDocument contentProjectXml, XmlNamespaceManager ns, XElement targetItemGroup, string itemName)
        {
            var targetElements = targetItemGroup.Elements(buildNamespace + itemName)
                .ToDictionary(e => e.Attribute("Include").Value.ToLower());
            targetElements.Values.ToList().ForEach(e => e.Remove());

            //Find all elements that are not the same
            foreach (var newSitecoreItem in contentProjectXml.XPathSelectElements(string.Format("/ms:Project/ms:ItemGroup/ms:{0}", itemName), ns))
            {
                //Only add the element if it isn't in the target project
                var include = newSitecoreItem.Attribute("Include").Value.ToLower();
                if (!targetElements.ContainsKey(include))
                {
                    targetElements.Add(include, newSitecoreItem);
                }
            }

            //Add the new list of Sitecore items in alpha order
            targetItemGroup.Add((from kvp in targetElements
                                 orderby kvp.Key
                                 select kvp.Value).ToArray());
        }

        private static XElement FindTargetItemGroup(XNamespace buildNamespace, XDocument targetProjectXml, XmlNamespaceManager ns, string itemName)
        {
            var targetItemGroup = targetProjectXml.XPathSelectElement(string.Format("/ms:Project/ms:ItemGroup[ms:{0}]", itemName), ns);

            if (targetItemGroup == null)
            {
                //If we can't find the items, then maybe it is an empty project
                //Put the new item group at the end of the project
                var lastItemGroup = targetProjectXml.XPathSelectElements("/ms:Project/ms:ItemGroup", ns).Last();

                targetItemGroup = new XElement(buildNamespace + "ItemGroup");

                if (lastItemGroup == null)
                {
                    //If there are no item groups, put it at the end.
                    targetProjectXml.Element(buildNamespace + "Project").Add(targetItemGroup);
                }
                else
                {
                    lastItemGroup.AddAfterSelf(targetItemGroup);
                }
            }
            return targetItemGroup;
        }

        public void CopyFiles(string targetLocation, string sourceLocation)
        {
            //Validate the parameters
            if (targetLocation == null)
                throw new ArgumentNullException("targetLocation");

            if (sourceLocation == null)
                throw new ArgumentNullException("sourceLocation");

            var fullPath = Path.GetFullPath(sourceLocation);
            if (!Directory.Exists(sourceLocation))
                throw new FileNotFoundException(sourceLocation);

            //Make sure the source location doesn't end with a slash
            if (sourceLocation.EndsWith("\\"))
                sourceLocation = sourceLocation.Substring(0, sourceLocation.Length - 1);

            //Collect all the files in the starting location
            var filesToCopy = new List<string>();
            CollectFilesToCopy(sourceLocation, filesToCopy);

            //Copy the source files
            foreach (var sourceFile in filesToCopy)
            {
                var targetFileName = Path.Combine(targetLocation, sourceFile.Substring(sourceLocation.Length + 1));

                //Skip any files that exist
                if (!File.Exists(targetFileName))
                {
                    var targetDirectory = Path.GetDirectoryName(targetFileName);

                    EnsureTargetDirectory(targetDirectory);

                    File.Copy(sourceFile, targetFileName);
                }
            }
        }

        public void UpdateExcludedFiles(string contentProject)
        {
            if (contentProject == null)
                throw new ArgumentNullException("contentProject");

            XNamespace buildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

            //Load the projects
            var contentProjectXml = XDocument.Load(contentProject);

            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("ms", buildNamespace.NamespaceName);

            //Find the Element that holds all the SitecoreItem elements
            var excludedAssembliesContainer = contentProjectXml.XPathSelectElement("/ms:Project/ms:ItemGroup[ms:ExcludedAssemblies]", ns);

            if (excludedAssembliesContainer == null)
            {
                var projectContainer = contentProjectXml.XPathSelectElement("/ms:Project", ns);

                excludedAssembliesContainer = new XElement(buildNamespace + "ItemGroup");
                projectContainer.Add(excludedAssembliesContainer);
            }
            else
            {
                excludedAssembliesContainer.RemoveAll();
            }

            var excludedAssembly = new XElement(buildNamespace + "ExcludedAssemblies");
            excludedAssembly.Add(new XAttribute("Include", "Sitecore.Kernel.dll"));
            excludedAssembliesContainer.Add(excludedAssembly);

            excludedAssembly = new XElement(buildNamespace + "ExcludedAssemblies");
            excludedAssembly.Add(new XAttribute("Include", "Sitecore.Client.dll"));
            excludedAssembliesContainer.Add(excludedAssembly);

            excludedAssembly = new XElement(buildNamespace + "ExcludedAssemblies");
            excludedAssembly.Add(new XAttribute("Include", "Sitecore.Analytics.dll"));
            excludedAssembliesContainer.Add(excludedAssembly);

            excludedAssembly = new XElement(buildNamespace + "ExcludedAssemblies");
            excludedAssembly.Add(new XAttribute("Include", "Lucene.Net.dll"));
            excludedAssembliesContainer.Add(excludedAssembly);

            contentProjectXml.Save(contentProject);
        }

        /// <summary>
        /// Make sure the path to the files exists
        /// </summary>
        /// <param name="targetDirectory"></param>
        private static void EnsureTargetDirectory(string targetDirectory)
        {
            if (!Directory.Exists(targetDirectory))
            {
                EnsureTargetDirectory(Path.GetDirectoryName(targetDirectory));

                Directory.CreateDirectory(targetDirectory);
            }
        }

        /// <summary>
        /// Collect the files we are going to copy from the source directoy
        /// </summary>
        /// <param name="sourceLocation"></param>
        /// <param name="filesToCopy"></param>
        private static void CollectFilesToCopy(string sourceLocation, List<string> filesToCopy)
        {
            //Get the files
            filesToCopy.AddRange(Directory.GetFiles(sourceLocation).Where(f => f.EndsWith(".item") || f.EndsWith(".tt")));

            //Get the files in all the directories
            foreach (string subdir in Directory.GetDirectories(sourceLocation))
            {
                CollectFilesToCopy(subdir, filesToCopy);
            }
        }
    }
}
