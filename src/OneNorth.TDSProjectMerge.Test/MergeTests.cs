using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace OneNorth.TDSProjectMerge.Test
{
    [TestClass]
    public class MergeTests
    {
        [TestMethod]
        [DeploymentItem(".\\Resources", ".\\Resources")]
        public void LocalTestMerge()
        {
            //Create the work folder
            if (!Directory.Exists(".\\Work"))
            {
                Directory.CreateDirectory(".\\Work");
            }

            File.Copy(".\\Resources\\ContentProject.scproj", ".\\Work\\ContentProject.scproj", true);
            File.Copy(".\\Resources\\TargetProject.scproj", ".\\Work\\TargetProject.scproj", true);

            var merger = new MergeTask();

            merger.MergeProjects(".\\Work\\TargetProject.scproj", ".\\Work\\ContentProject.scproj");
        }

        [TestMethod]
        [DeploymentItem(".\\Resources", ".\\Resources")]
        public void LocalTestMergeToEmptyProject()
        {
            //Create the work folder
            if (!Directory.Exists(".\\Work"))
            {
                Directory.CreateDirectory(".\\Work");
            }

            File.Copy(".\\Resources\\ContentProject.scproj", ".\\Work\\ContentProject.scproj", true);
            File.Copy(".\\Resources\\EmptyTargetProject.scproj", ".\\Work\\EmptyTargetProject.scproj", true);

            var merger = new MergeTask();

            merger.MergeProjects(".\\Work\\EmptyTargetProject.scproj", ".\\Work\\ContentProject.scproj");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestMergeNullTarget()
        {
            var merger = new MergeTask();

            merger.MergeProjects(null, "?");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestMergeNullContent()
        {
            var merger = new MergeTask();

            merger.MergeProjects("?", null);
        }

        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public void TestMergeMissingTarget()
        {
            var merger = new MergeTask();

            merger.MergeProjects("x", "?");
        }

        [TestMethod]
        [DeploymentItem(".\\Resources", ".\\Resources")]
        [ExpectedException(typeof(FileNotFoundException))]
        public void TestMergeMissingContent()
        {
            //Create the work folder
            if (!Directory.Exists(".\\Work"))
            {
                Directory.CreateDirectory(".\\Work");
            }

            File.Copy(".\\Resources\\EmptyTargetProject.scproj", ".\\Work\\EmptyTargetProject.scproj", true);

            var merger = new MergeTask();

            merger.MergeProjects(".\\Work\\EmptyTargetProject.scproj", "x");
        }

        [TestMethod]
        [DeploymentItem(".\\Resources", ".\\Resources")]
        public void CopyFiles()
        {
            //Create the work folder
            if (Directory.Exists(".\\Work"))
            {
                Directory.Delete(".\\Work", true);
            }

            var merger = new MergeTask();

            merger.CopyFiles(".\\Work", ".\\Resources\\TestItems");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CopyFilesNullTarget()
        {
            var merger = new MergeTask();

            merger.CopyFiles(null, "x");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CopyFilesNullSource()
        {
            var merger = new MergeTask();

            merger.CopyFiles("x", null);
        }


        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public void CopyFilesMissingSource()
        {
            var merger = new MergeTask();

            merger.CopyFiles("x", "x");
        }

        [TestMethod]
        [DeploymentItem(".\\Resources", ".\\Resources")]
        public void TestUpdateExcludedAssemblies()
        {
            //Create the work folder
            if (!Directory.Exists(".\\Work"))
            {
                Directory.CreateDirectory(".\\Work");
            }

            File.Copy(".\\Resources\\ContentProject.scproj", ".\\Work\\ContentProject.scproj", true);

            var merger = new MergeTask();
            merger.UpdateExcludedFiles(".\\Work\\ContentProject.scproj");
        }
    }
}
