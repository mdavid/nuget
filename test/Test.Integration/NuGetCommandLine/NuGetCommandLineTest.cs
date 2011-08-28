using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NuGet.Test.Integration.NuGetCommandLine {
    [TestClass]
    public class NuGetCommandLineTest {
        private const string NoSpecsfolder = @".\nospecs\";
        private const string OneSpecfolder = @".\onespec\";
        private const string TwoSpecsFolder = @".\twospecs\";
        private const string OutputFolder = @".\output\";
        private const string SpecificFilesFolder = @".\specific_files\";
        private const string ProjectFilesFolder = @".\projects\";
        private const string NugetExePath = @".\NuGet.exe";

        private StringWriter consoleOutput;
        private TextWriter originalConsoleOutput;
        private TextWriter originalErrorConsoleOutput;
        private string startingDirectory;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context) {
            Program.IgnoreExtensions = true;
        }

        [ClassCleanup]
        public static void ClassCleanup() {
            Program.IgnoreExtensions = false;
        }

        [TestInitialize]
        public void Initialize() {
            DeleteDirs();

            Directory.CreateDirectory(NoSpecsfolder);
            Directory.CreateDirectory(OneSpecfolder);
            Directory.CreateDirectory(TwoSpecsFolder);
            Directory.CreateDirectory(SpecificFilesFolder);
            Directory.CreateDirectory(OutputFolder);
            Directory.CreateDirectory(ProjectFilesFolder);

            originalConsoleOutput = System.Console.Out;
            originalErrorConsoleOutput = System.Console.Error;
            consoleOutput = new StringWriter();
            System.Console.SetOut(consoleOutput);
            System.Console.SetError(consoleOutput);
            startingDirectory = Directory.GetCurrentDirectory();
        }

        [TestCleanup]
        public void Cleanup() {
            DeleteDirs();
            System.Console.SetOut(originalConsoleOutput);
            System.Console.SetError(originalErrorConsoleOutput);
            Directory.SetCurrentDirectory(startingDirectory);
        }


        [TestMethod]
        public void NuGetCommandLine_ShowsHelpIfThereIsNoCommand() {
            // Arrange 
            string[] args = new string[0];

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("usage: NuGet <command> [args] [options]"));
        }

        [TestMethod]
        public void PackageCommand_ThrowsWhenPassingNoArgsAndThereAreNoNuSpecFiles() {
            // Arrange 
            string[] args = new string[] { "pack" };
            Directory.SetCurrentDirectory(NoSpecsfolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(1, result);
            Assert.AreEqual("Please specify a nuspec or project file to use.", consoleOutput.ToString().Trim());
        }

        [TestMethod]
        public void PackageCommand_ThrowsWhenPassingNoArgsAndThereIsMoreThanOneNuSpecFile() {
            // Arrange
            string nuspecFile = Path.Combine(TwoSpecsFolder, "antlr.nuspec");
            File.WriteAllText(nuspecFile, NuSpecFileContext.FileContents);
            string nuspecFile2 = Path.Combine(TwoSpecsFolder, "antlr2.nuspec");
            File.WriteAllText(nuspecFile2, NuSpecFileContext.FileContents);
            string[] args = new string[] { "pack" };
            Directory.SetCurrentDirectory(TwoSpecsFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(1, result);
            Assert.AreEqual("Please specify a nuspec or project file to use.", consoleOutput.ToString().Trim());
        }

        [TestMethod]
        public void PackageCommand_CreatesPackageWhenPassingNoArgsAndThereOneNuSpecFile() {
            //Arrange
            string nuspecFile = Path.Combine(OneSpecfolder, "antlr.nuspec");
            File.WriteAllText(nuspecFile, NuSpecFileContext.FileContents);
            File.WriteAllText(Path.Combine(OneSpecfolder, "foo.txt"), "test");
            string[] args = new string[] { "pack" };
            Directory.SetCurrentDirectory(OneSpecfolder);

            //Act
            int result = Program.Main(args);

            //Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Successfully created package"));
        }

        [TestMethod]
        public void PackageCommand_CreatesPackageWhenPassingBasePath() {
            //Arrange
            string nuspecFile = Path.Combine(OneSpecfolder, "Antlr.nuspec");
            string expectedPackage = Path.Combine("..\\output\\", "Antlr.3.1.1.nupkg");
            File.WriteAllText(Path.Combine(OneSpecfolder, "foo.txt"), "test");
            File.WriteAllText(nuspecFile, NuSpecFileContext.FileContents);
            string[] args = new string[] { "pack", "-o", "..\\output\\" };
            Directory.SetCurrentDirectory(OneSpecfolder);

            //Act
            int result = Program.Main(args);

            //Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Successfully created package"));
            Assert.IsTrue(File.Exists(expectedPackage));
        }

        [TestMethod]
        public void PackageCommand_ShowConsistentErrorMessageWhenNuspecHasInvalidID1() {
            // Arrange            
            string nuspecFile = Path.Combine(SpecificFilesFolder, "InvalidId.nuspec");
            string expectedPackage = "InvalidId.nupkg";
            File.WriteAllText(nuspecFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>test\id</id>
    <version>1.0</version>
    <authors>Terence Parr</authors>
    <description>ANother Tool for Language Recognition, is a language tool that provides a framework for constructing recognizers, interpreters, compilers, and translators from grammatical descriptions containing actions in a variety of target languages.</description>
    <language>en-US</language>
  </metadata>
  <files>
    <file src=""file1.txt"" target=""content"" />
  </files>
</package>");

            string[] args = new string[] { "pack" };
            Directory.SetCurrentDirectory(SpecificFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(1, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Attempting to build package from 'InvalidId.nuspec'.\r\nThe package ID 'test\\id' contains invalid characters. Examples of valid package IDs include 'MyPackage' and 'MyPackage.Sample'.\r\n"));
            Assert.IsFalse(File.Exists(expectedPackage));
        }

        [TestMethod]
        public void PackageCommand_ShowConsistentErrorMessageWhenNuspecHasInvalidID2() {
            // Arrange            
            string nuspecFile = Path.Combine(SpecificFilesFolder, "InvalidId.nuspec");
            string expectedPackage = "InvalidId.nupkg";
            File.WriteAllText(nuspecFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>test:id</id>
    <version>1.0</version>
    <authors>Terence Parr</authors>
    <description>ANother Tool for Language Recognition, is a language tool that provides a framework for constructing recognizers, interpreters, compilers, and translators from grammatical descriptions containing actions in a variety of target languages.</description>
    <language>en-US</language>
  </metadata>
  <files>
    <file src=""file1.txt"" target=""content"" />
  </files>
</package>");

            string[] args = new string[] { "pack" };
            Directory.SetCurrentDirectory(SpecificFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(1, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Attempting to build package from 'InvalidId.nuspec'.\r\nThe package ID 'test:id' contains invalid characters. Examples of valid package IDs include 'MyPackage' and 'MyPackage.Sample'.\r\n"));
            Assert.IsFalse(File.Exists(expectedPackage));
        }

        [TestMethod]
        public void PackageCommand_ShowConsistentErrorMessageWhenNuspecHasInvalidID3() {
            // Arrange            
            string nuspecFile = Path.Combine(SpecificFilesFolder, "InvalidId.nuspec");
            string expectedPackage = "InvalidId.nupkg";
            File.WriteAllText(nuspecFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>test|id</id>
    <version>1.0</version>
    <authors>Terence Parr</authors>
    <description>ANother Tool for Language Recognition, is a language tool that provides a framework for constructing recognizers, interpreters, compilers, and translators from grammatical descriptions containing actions in a variety of target languages.</description>
    <language>en-US</language>
  </metadata>
  <files>
    <file src=""file1.txt"" target=""content"" />
  </files>
</package>");

            string[] args = new string[] { "pack" };
            Directory.SetCurrentDirectory(SpecificFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(1, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Attempting to build package from 'InvalidId.nuspec'.\r\nThe package ID 'test|id' contains invalid characters. Examples of valid package IDs include 'MyPackage' and 'MyPackage.Sample'.\r\n"));
            Assert.IsFalse(File.Exists(expectedPackage));
        }

        [TestMethod]
        public void PackageCommand_ShowConsistentErrorMessageWhenNuspecHasInvalidID4() {
            // Arrange            
            string nuspecFile = Path.Combine(SpecificFilesFolder, "InvalidId.nuspec");
            string expectedPackage = "InvalidId.nupkg";
            File.WriteAllText(nuspecFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>test/id</id>
    <version>1.0</version>
    <authors>Terence Parr</authors>
    <description>ANother Tool for Language Recognition, is a language tool that provides a framework for constructing recognizers, interpreters, compilers, and translators from grammatical descriptions containing actions in a variety of target languages.</description>
    <language>en-US</language>
  </metadata>
  <files>
    <file src=""file1.txt"" target=""content"" />
  </files>
</package>");

            string[] args = new string[] { "pack" };
            Directory.SetCurrentDirectory(SpecificFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(1, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Attempting to build package from 'InvalidId.nuspec'.\r\nThe package ID 'test/id' contains invalid characters. Examples of valid package IDs include 'MyPackage' and 'MyPackage.Sample'.\r\n"));
            Assert.IsFalse(File.Exists(expectedPackage));
        }

        [TestMethod]
        public void PackageCommand_SpecifyingFilesInNuspecOnlyPackagesSpecifiedFiles() {
            // Arrange            
            string nuspecFile = Path.Combine(SpecificFilesFolder, "SpecWithFiles.nuspec");
            string expectedPackage = "test.1.1.1.nupkg";
            File.WriteAllText(Path.Combine(SpecificFilesFolder, "file1.txt"), "file 1");
            File.WriteAllText(Path.Combine(SpecificFilesFolder, "file2.txt"), "file 2");
            File.WriteAllText(Path.Combine(SpecificFilesFolder, "file3.txt"), "file 3");
            File.WriteAllText(nuspecFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>test</id>
    <version>1.1.1</version>
    <authors>Terence Parr</authors>
    <description>ANother Tool for Language Recognition, is a language tool that provides a framework for constructing recognizers, interpreters, compilers, and translators from grammatical descriptions containing actions in a variety of target languages.</description>
    <language>en-US</language>
  </metadata>
  <files>
    <file src=""file1.txt"" target=""content"" />
  </files>
</package>");
            string[] args = new string[] { "pack" };
            Directory.SetCurrentDirectory(SpecificFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Successfully created package"));
            Assert.IsTrue(File.Exists(expectedPackage));

            VerifyPackageContents(expectedPackage, new[] { @"content\file1.txt" });
        }

        [TestMethod]
        public void PackageCommand_SpecifyingEmptyFilesElementInNuspecPackagesNoFiles() {
            // Arrange            
            string nuspecFile = Path.Combine(SpecificFilesFolder, "SpecWithFiles.nuspec");
            string expectedPackage = "empty.2.2.2.nupkg";
            File.WriteAllText(Path.Combine(SpecificFilesFolder, "file1.txt"), "file 1");
            File.WriteAllText(Path.Combine(SpecificFilesFolder, "file2.txt"), "file 2");
            File.WriteAllText(Path.Combine(SpecificFilesFolder, "file3.txt"), "file 3");
            File.WriteAllText(nuspecFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>empty</id>
    <version>2.2.2</version>
    <authors>Terence Parr</authors>
    <description>ANother Tool for Language Recognition, is a language tool that provides a framework for constructing recognizers, interpreters, compilers, and translators from grammatical descriptions containing actions in a variety of target languages.</description>
    <language>en-US</language>
    <dependencies>
      <dependency id=""aaa"" />
    </dependencies>
  </metadata>
  <files />
</package>");
            string[] args = new string[] { "pack" };
            Directory.SetCurrentDirectory(SpecificFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Successfully created package"));
            Assert.IsTrue(File.Exists(expectedPackage));

            VerifyPackageContents(expectedPackage, new string[0]);
        }

        [TestMethod]
        public void PackageCommand_AcceptEmptyDependenciesElement() {
            // Arrange            
            string nuspecFile = Path.Combine(SpecificFilesFolder, "SpecWithFiles.nuspec");
            string expectedPackage = "dep.2.2.2.nupkg";
            File.WriteAllText(Path.Combine(SpecificFilesFolder, "file1.txt"), "file 1");
            File.WriteAllText(nuspecFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>dep</id>
    <version>2.2.2</version>
    <authors>Terence Parr</authors>
    <description>ANother Tool for Language Recognition, is a language tool that provides a framework for constructing recognizers, interpreters, compilers, and translators from grammatical descriptions containing actions in a variety of target languages.</description>
    <language>en-US</language>
    <dependencies>
    </dependencies>
  </metadata>
</package>");
            string[] args = new string[] { "pack" };
            Directory.SetCurrentDirectory(SpecificFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Successfully created package"));
            Assert.IsTrue(File.Exists(expectedPackage));

            ZipPackage package = VerifyPackageContents(expectedPackage, new[] { @"file1.txt" });

            Assert.IsFalse(package.Dependencies.Any());
        }

        [TestMethod]
        public void PackageCommand_AcceptEmptyFrameworkAssemblyElement() {
            // Arrange            
            string nuspecFile = Path.Combine(SpecificFilesFolder, "SpecWithFiles.nuspec");
            string expectedPackage = "framework.2.2.2.nupkg";
            File.WriteAllText(Path.Combine(SpecificFilesFolder, "file1.txt"), "file 1");
            File.WriteAllText(nuspecFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>framework</id>
    <version>2.2.2</version>
    <authors>Terence Parr</authors>
    <description>ANother Tool for Language Recognition, is a language tool that provides a framework for constructing recognizers, interpreters, compilers, and translators from grammatical descriptions containing actions in a variety of target languages.</description>
    <language>en-US</language>
    <frameworkAssemblies>
    </frameworkAssemblies>
  </metadata>
</package>");
            string[] args = new string[] { "pack" };
            Directory.SetCurrentDirectory(SpecificFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Successfully created package"));
            Assert.IsTrue(File.Exists(expectedPackage));

            ZipPackage package = VerifyPackageContents(expectedPackage, new[] { @"file1.txt" });

            Assert.IsFalse(package.FrameworkAssemblies.Any());
        }

        [TestMethod]
        public void PackageCommand_SpecifyingProjectFileCreatesPackageAndSymbolsPackge() {
            // Arrange            
            string expectedPackage = "FakeProject.1.2.nupkg";
            string expectedSymbolsPackage = "FakeProject.1.2.symbols.nupkg";

            WriteProjectFile("Runner.cs", @"using System;
public class Runner { 
    public static void Run() { 
        Console.WriteLine(""Hello World"");
    }
}");
            WriteProjectFile(@"..\Foo.cs", @"using System;
public class Foo { 
    public static void Run() { 
        Console.WriteLine(""Hello World"");
    }
}");
            WriteProjectFile(@"Bar.cs", @"using System;
public class Bar { 
    public static void Run() { 
        Console.WriteLine(""Hello World"");
    }
}");
            WriteProjectFile(@"..\Baz.cs", @"using System;
public class Baz { 
    public static void Run() { 
        Console.WriteLine(""Hello World"");
    }
}");
            WriteAssemblyInfo("FakeProject",
                               "1.2.0.0",
                               "David Inc",
                               "This is a test. Ignore me");

            CreateProject("FakeProject",
                          compile: new[] { "Runner.cs", @"..\Foo.cs", @"..\projects\Bar.cs" },
                          links: new[] { Tuple.Create(@"..\Baz.cs", @"Folder\Baz.cs") });

            string[] args = new string[] { "pack", "-Symbols", "-Build" };
            Directory.SetCurrentDirectory(ProjectFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Successfully created package"));
            Assert.IsTrue(File.Exists(expectedPackage));
            var package = VerifyPackageContents(expectedPackage, new[] { @"lib\net40\FakeProject.dll" });
            Assert.AreEqual("FakeProject", package.Id);
            Assert.AreEqual(new Version("1.2"), package.Version);
            Assert.AreEqual("David Inc", package.Authors.First());
            Assert.AreEqual("This is a test. Ignore me", package.Description);
            Assert.IsTrue(File.Exists(expectedSymbolsPackage));
            VerifyPackageContents(expectedSymbolsPackage, new[] { @"src\Foo.cs",
                                                                  @"src\Runner.cs",
                                                                  @"src\Folder\Baz.cs",
                                                                  @"src\Bar.cs",
                                                                  @"src\Properties\AssemblyInfo.cs",
                                                                  @"lib\net40\FakeProject.dll",
                                                                  @"lib\net40\FakeProject.pdb" });
        }


        [TestMethod]
        public void PackageCommand_SpecifyingProjectFilePacksContentAndOutput() {
            // Arrange                        
            string expectedPackage = "ProjectWithCotent.1.5.nupkg";
            var contentFiles = new[] { "Foo.xml", "Bar.txt" };
            var sourceFiles = new[] { "A.cs", "B.cs" };

            foreach (var contentFile in contentFiles) {
                WriteProjectFile(contentFile, contentFile);
            }

            int index = 0;
            foreach (var sourceFile in sourceFiles) {
                WriteProjectFile(sourceFile, String.Format(@"using System;
public class Cl_{0} {{
    public void Foo() {{ }}
}}
", index++));
            }

            WriteAssemblyInfo("ProjectWithCotent",
                               "1.5.0.0",
                               "David",
                               "Project with content");

            CreateProject("ProjectWithCotent", content: contentFiles, compile: sourceFiles);

            string[] args = new string[] { "pack", "-Build" };
            Directory.SetCurrentDirectory(ProjectFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Successfully created package"));
            Assert.IsTrue(File.Exists(expectedPackage));

            var package = VerifyPackageContents(expectedPackage, new[] { @"lib\net40\ProjectWithCotent.dll",
                                                                         @"content\Foo.xml",
                                                                         @"content\Bar.txt" });
            Assert.AreEqual("ProjectWithCotent", package.Id);
            Assert.AreEqual(new Version("1.5"), package.Version);
            Assert.AreEqual("David", package.Authors.First());
            Assert.AreEqual("Project with content", package.Description);
        }

        [TestMethod]
        public void PackageCommand_TestDefaultPackageIssueRules() {
            //Arrange
            string nuspecFile = Path.Combine(OneSpecfolder, "beta.nuspec");
            File.WriteAllText(nuspecFile, NuSpecFileContext.FileContents);
            
            // violated rule: Invalid Framework Folder
            Directory.CreateDirectory(Path.Combine(OneSpecfolder, "lib"));
            Directory.CreateDirectory(Path.Combine(OneSpecfolder, "lib", "unknown"));
            File.WriteAllText(Path.Combine(OneSpecfolder, "lib\\unknown\\abc.dll"), "assembly");

            // violated rule: Assembly placed directly under lib
            File.WriteAllText(Path.Combine(OneSpecfolder, "lib\\def.dll"), "assembly");

            // violated rule: Assembly placed directly under lib
            Directory.CreateDirectory(Path.Combine(OneSpecfolder, "content"));
            File.WriteAllText(Path.Combine(OneSpecfolder, "content\\hello.dll"), "assembly");

            // violated rule: Script file placed outside tools
            File.WriteAllText(Path.Combine(OneSpecfolder, "install.ps1"), "script");

            // violated rule: Unrecognized script file
            Directory.CreateDirectory(Path.Combine(OneSpecfolder, "tools"));
            File.WriteAllText(Path.Combine(OneSpecfolder, "tools\\myscript.ps1"), "script");

            // violated rule: transform file outside content folder
            File.WriteAllText(Path.Combine(OneSpecfolder, "tools\\web.config.transform"), "transform");

            // violated rule: non-assembly inside lib
            File.WriteAllText(Path.Combine(OneSpecfolder, "lib\\mylibrary.xml"), "xml");

            string[] args = new string[] { "pack", "-RunPackageAnalysis" };
            Directory.SetCurrentDirectory(OneSpecfolder);

            //Act
            int result = Program.Main(args);

            //Assert
            Assert.AreEqual(0, result);
            string output = consoleOutput.ToString();
            Assert.IsTrue(output.Contains("Successfully created package"));

            // Asserts for package issues
            Assert.IsTrue(output.Contains("7 issue(s) found with the generated package."));
            Assert.IsTrue(output.Contains("Incompatible files in lib folder"));
            Assert.IsTrue(output.Contains("Invalid framework folder"));
            Assert.IsTrue(output.Contains("Assembly not inside a framework folder"));
            Assert.IsTrue(output.Contains("Assembly outside lib folder"));
            Assert.IsTrue(output.Contains("PowerScript file outside tools folder"));
            Assert.IsTrue(output.Contains("Unrecognized PowerScript file"));
            Assert.IsTrue(output.Contains("Incompatible files in lib folder"));
        }

        [TestMethod]
        public void PackageCommand_SpecifyingProjectFileWithNuSpecWithTokensSubstitutesMetadataFromProject() {
            // Arrange
            string expectedPackage = "ProjectWithNuSpec.1.2.nupkg";
            WriteAssemblyInfo("ProjectWithNuSpec",
                               "1.2.0.0",
                               "David",
                               "Project with content",
                               "Title of Package");

            WriteProjectFile("foo.cs", "public class Foo { }");
            WriteProjectFile("package.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>$id$</id>
    <title>$title$</title>
    <version>$version$</version>
    <authors>$author$</authors>
    <description>Description from nuspec</description>
    <language>fr-FR</language>
    <tags>t1 t2</tags>
    <dependencies>
        <dependency id=""elmah"" version=""1.5"" />
    </dependencies>
    <frameworkAssemblies>
        <frameworkAssembly assemblyName=""System.Web"" />
    </frameworkAssemblies>
  </metadata>
</package>");

            CreateProject("ProjectWithNuSpec", content: new[] { "package.nuspec" },
                                               compile: new[] { "foo.cs" });

            string[] args = new string[] { "pack", "ProjectWithNuSpec.csproj", "-Build" };
            Directory.SetCurrentDirectory(ProjectFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Successfully created package"));
            Assert.IsTrue(File.Exists(expectedPackage));

            var package = VerifyPackageContents(expectedPackage, new[] { @"lib\net40\ProjectWithNuSpec.dll" });
            Assert.AreEqual("ProjectWithNuSpec", package.Id);
            Assert.AreEqual(new Version("1.2"), package.Version);
            Assert.AreEqual("Title of Package", package.Title);
            Assert.AreEqual("David", package.Authors.First());
            Assert.AreEqual("Description from nuspec", package.Description);
            var dependencies = package.Dependencies.ToList();
            Assert.AreEqual(1, dependencies.Count);
            Assert.AreEqual("elmah", dependencies[0].Id);
            var frameworkAssemblies = package.FrameworkAssemblies.ToList();
            Assert.AreEqual("System.Web", frameworkAssemblies[0].AssemblyName);
        }

        [TestMethod]
        public void PackageCommand_SpecifyingProjectFileWithNuSpecNamedAfterProjectUsesNuSpecForMetadata() {
            // Arrange                        
            string expectedPackage = "Test.1.2.nupkg";
            WriteAssemblyInfo("FooProject",
                               "1.5.0.0",
                               "David",
                               "Project with content");

            WriteProjectFile("foo.cs", "public class Foo { }");
            WriteProjectFile("FooProject.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>Test</id>
    <version>1.2</version>
    <description>Description from nuspec</description>    
    <authors>John</authors>
  </metadata>
</package>");

            CreateProject("FooProject", compile: new[] { "foo.cs" });

            string[] args = new string[] { "pack", "FooProject.csproj", "-Build" };
            Directory.SetCurrentDirectory(ProjectFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Successfully created package"));
            Assert.IsTrue(File.Exists(expectedPackage));

            var package = VerifyPackageContents(expectedPackage, new[] { @"lib\net40\FooProject.dll" });
            Assert.AreEqual("Test", package.Id);
            Assert.AreEqual(new Version("1.2"), package.Version);
            Assert.AreEqual("Description from nuspec", package.Description);
            Assert.AreEqual("John", package.Authors.First());
        }

        [TestMethod]
        public void PackageCommand_SpecifyingProjectFileWithNoBuildThrowsIfProjectNotBuilt() {
            // Arrange                        
            WriteAssemblyInfo("ProjectNoBuild",
                               "1.5.0.0",
                               "David",
                               "Project with content");

            WriteProjectFile("foo.cs", "public class Foo { }");
            CreateProject("ProjectNoBuild", compile: new[] { "foo.cs" });

            string[] args = new string[] { "pack", "ProjectNoBuild.csproj" };
            Directory.SetCurrentDirectory(ProjectFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(1, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Make sure the project has been built."));
        }

        [TestMethod]
        public void PackageCommand_SpecifyingProjectFileWithNuSpecWithUnsupportedTokensThrows() {
            // Arrange                        
            string expectedPackage = "ProjectWithBrokenNuSpec.1.2.nupkg";
            WriteAssemblyInfo("ProjectWithBrokenNuSpec",
                               "1.2.0.0",
                               "David",
                               "Project with content");

            WriteProjectFile("foo.cs", "public class Foo { }");
            WriteProjectFile("package.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>$id2$</id>
    <version>$version$</version>
    <authors>$author$</authors>
    <description>Description from nuspec</description>
  </metadata>
</package>");

            CreateProject("ProjectWithBrokenNuSpec", content: new[] { "package.nuspec" },
                                               compile: new[] { "foo.cs" });

            string[] args = new string[] { "pack", "ProjectWithBrokenNuSpec.csproj", "-Build" };
            Directory.SetCurrentDirectory(ProjectFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(1, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("The replacement token 'id2' has no value."));
            Assert.IsFalse(File.Exists(expectedPackage));
        }

        [TestMethod]
        public void PackageCommand_SpecifyingProjectFileAndNuSpecWithFilesMergesFiles() {
            // Arrange                        
            string expectedPackage = "ProjectWithNuSpecAndFiles.1.3.nupkg";
            WriteAssemblyInfo("ProjectWithNuSpecAndFiles",
                               "1.3.0.0",
                               "David2",
                               "Project with nuspec that has files");

            WriteProjectFile("foo.cs", "public class Foo { }");
            WriteProjectFile("package.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>$AssemblyName$</id>   
    <version>$version$</version>
    <description>$description$</description>
    <authors>$author$</authors>
  </metadata>
  <files>
    <file src=""bin\Debug\*.dll"" target=""lib\net40"" />
    <file src=""bin\Debug\*.pdb"" target=""lib\net40"" />
    <file src=""bin\Debug\*.xml"" target=""lib\net40"" />
  </files>
</package>");

            CreateProject("ProjectWithNuSpecAndFiles", content: new[] { "package.nuspec" },
                                                       compile: new[] { "foo.cs" });

            string[] args = new string[] { "pack", "ProjectWithNuSpecAndFiles.csproj", "-Build" };
            Directory.SetCurrentDirectory(ProjectFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Successfully created package"));
            Assert.IsTrue(File.Exists(expectedPackage));

            var package = VerifyPackageContents(expectedPackage, new[] { @"lib\net40\ProjectWithNuSpecAndFiles.dll", 
                                                                         @"lib\net40\ProjectWithNuSpecAndFiles.pdb" });
            Assert.AreEqual("ProjectWithNuSpecAndFiles", package.Id);
            Assert.AreEqual(new Version("1.3"), package.Version);
            Assert.AreEqual("David2", package.Authors.First());
            Assert.AreEqual("Project with nuspec that has files", package.Description);
        }

        [TestMethod]
        public void PackageCommand_PrefersProjectFileIfNuSpecAndProjectFileAreInTheSameDirectory() {
            // Arrange                        
            string expectedPackage = "ProjectWithNuSpecProjectWins.1.2.nupkg";
            WriteAssemblyInfo("ProjectWithNuSpecProjectWins",
                               "1.2.0.0",
                               "David2",
                               "Project with nuspec");

            WriteProjectFile("foo.cs", "public class Foo { }");
            WriteProjectFile("package.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>$AssemblyName$</id>   
    <version>$version$</version>
    <description>$description$</description>
    <authors>$author$</authors>
  </metadata>
</package>");

            CreateProject("ProjectWithNuSpecProjectWins", content: new[] { "package.nuspec" },
                                                       compile: new[] { "foo.cs" });

            string[] args = new string[] { "pack", "-Build" };
            Directory.SetCurrentDirectory(ProjectFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Successfully created package"));
            Assert.IsTrue(File.Exists(expectedPackage));

            var package = VerifyPackageContents(expectedPackage, new[] { @"lib\net40\ProjectWithNuSpecProjectWins.dll" });
            Assert.AreEqual("ProjectWithNuSpecProjectWins", package.Id);
            Assert.AreEqual(new Version("1.2"), package.Version);
            Assert.AreEqual("David2", package.Authors.First());
            Assert.AreEqual("Project with nuspec", package.Description);
        }

        [TestMethod]
        public void PackageCommand_SpecifyingProjectOnlyPacksAssemblyThatProjectProduced() {
            // Arrange                        
            string expectedPackage = "ProjectWithAssembliesInOutputPath.1.3.nupkg";
            WriteAssemblyInfo("ProjectWithAssembliesInOutputPath",
                               "1.3.0.0",
                               "David2",
                               "Project with nuspec that has files");

            WriteProjectFile("foo.cs", "public class Foo { }");
            WriteProjectFile(@"bin\Release\Fake.dll", "Some fakedll");

            CreateProject("ProjectWithAssembliesInOutputPath", compile: new[] { "foo.cs" });

            string[] args = new string[] { "pack", "ProjectWithAssembliesInOutputPath.csproj", "-Build" };
            Directory.SetCurrentDirectory(ProjectFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("Successfully created package"));
            Assert.IsTrue(File.Exists(expectedPackage));

            var package = VerifyPackageContents(expectedPackage, new[] { @"lib\net40\ProjectWithAssembliesInOutputPath.dll" });
            Assert.AreEqual("ProjectWithAssembliesInOutputPath", package.Id);
            Assert.AreEqual(new Version("1.3"), package.Version);
            Assert.AreEqual("David2", package.Authors.First());
            Assert.AreEqual("Project with nuspec that has files", package.Description);
        }

        [TestMethod]
        public void PackageCommand_WhenErrorIsThrownPackageFileIsDeleted() {
            // Arrange            
            string nuspecFile = Path.Combine(SpecificFilesFolder, "SpecWithErrors.nuspec");
            string expectedPackage = "hello world.1.1.1.nupkg";
            File.WriteAllText(nuspecFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>hello world</id>
    <version>1.1.1</version>
    <authors>Bar</authors>
    <description>Foo</description>
    <language>en-US</language>
  </metadata>
</package>");
            string[] args = new string[] { "pack" };
            Directory.SetCurrentDirectory(SpecificFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(1, result);
            Assert.IsFalse(File.Exists(expectedPackage));
        }


        [TestMethod]
        public void PackCommandAllowsPassingPropertiesFromCommandLine() {
            // Arrange            
            string nuspecFile = Path.Combine(SpecificFilesFolder, "SpecWithProperties.nuspec");
            string expectedPackage = "foo.1.1.nupkg";
            File.WriteAllText(Path.Combine(SpecificFilesFolder, "foo.txt"), "test");
            File.WriteAllText(nuspecFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>$id$</id>   
    <version>$version$</version>
    <description>Desc</description>
    <authors>Auth</authors>
  </metadata>
</package>");
            string[] args = new string[] { "pack", "/p", "id=foo;version=1.1" };
            Directory.SetCurrentDirectory(SpecificFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(File.Exists(expectedPackage));
            var package = VerifyPackageContents(expectedPackage, new[] { @"foo.txt" });
            Assert.AreEqual("foo", package.Id);
            Assert.AreEqual(new Version("1.1"), package.Version);
            Assert.AreEqual("Auth", package.Authors.First());
            Assert.AreEqual("Desc", package.Description);
        }

        [TestMethod]
        public void PackCommandAllowsPassingVersionSetsVersionProperty() {
            // Arrange            
            string nuspecFile = Path.Combine(SpecificFilesFolder, "SpecWithProperties.nuspec");
            string expectedPackage = "MyPackage.2.5.nupkg";
            File.WriteAllText(Path.Combine(SpecificFilesFolder, "foo.txt"), "test");
            File.WriteAllText(nuspecFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>MyPackage</id>   
    <version>$version$</version>
    <description>Desc</description>
    <authors>Auth</authors>
  </metadata>
</package>");
            string[] args = new string[] { "pack", "/version", "2.5" };
            Directory.SetCurrentDirectory(SpecificFilesFolder);

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(0, result);
            Assert.IsTrue(File.Exists(expectedPackage));
            var package = VerifyPackageContents(expectedPackage, new[] { @"foo.txt" });
            Assert.AreEqual("MyPackage", package.Id);
            Assert.AreEqual(new Version("2.5"), package.Version);
            Assert.AreEqual("Auth", package.Authors.First());
            Assert.AreEqual("Desc", package.Description);
        }

        [TestMethod]
        public void UpdateCommandThrowsWithNoArguments() {
            // Arrange            
            var args = new string[] { "update" };

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(1, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("No packages.config or solution file specified. Use the -self switch to update NuGet.exe."));
        }

        [TestMethod]
        public void UpdateCommandWithInvalidFileThrows() {
            // Arrange            
            var args = new string[] { "update", "lolz.txt" };

            // Act
            int result = Program.Main(args);

            // Assert
            Assert.AreEqual(1, result);
            Assert.IsTrue(consoleOutput.ToString().Contains("No packages.config or solution file specified."));
        }

        private ZipPackage VerifyPackageContents(string packageFile, IEnumerable<string> expectedFiles) {
            var package = new ZipPackage(packageFile);
            var files = package.GetFiles().Select(f => f.Path).OrderBy(f => f).ToList();
            CollectionAssert.AreEqual(expectedFiles.OrderBy(f => f).ToList(), files);
            return package;
        }

        private void CreateProject(string projectName, IEnumerable<string> content = null, IEnumerable<string> compile = null, IEnumerable<Tuple<string, string>> links = null) {
            string projectFile = Path.Combine(ProjectFilesFolder, projectName + ".csproj");
            File.WriteAllText(projectFile, GetProjectContent(projectName, compile: compile, content: content, links: links));
        }

        private static string GetProjectContent(string projectName,
                                                string targetFrameworkVersion = "4.0",
                                                IEnumerable<string> compile = null,
                                                IEnumerable<string> content = null,
                                                IEnumerable<Tuple<string, string>> links = null) {
            compile = compile ?? Enumerable.Empty<string>();
            content = content ?? Enumerable.Empty<string>();
            links = links ?? Enumerable.Empty<Tuple<string, string>>();
            string compileItemGroup = String.Join(Environment.NewLine, compile.Select(path => String.Format(@"<Compile Include=""{0}"" />", path)));
            string contentItemGroup = String.Join(Environment.NewLine, content.Select(path => String.Format(@"<Content Include=""{0}"" />", path)));
            string linkItemGroup = String.Join(Environment.NewLine, links.Select(link => String.Format(@"<Compile Include=""{0}""><Link>{1}</Link></Compile>", link.Item1, link.Item2)));
            return String.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    <ProductVersion>8.0.30703</ProductVersion>
    <SchemaVersion>2.0</SchemaVersion>
    <ProjectGuid>{{572A487C-B388-4490-B7E8-0382ABDAF729}}</ProjectGuid>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>{0}</RootNamespace>
    <AssemblyName>{0}</AssemblyName>
    <TargetFrameworkVersion>v{1}</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""System"" />
    <Reference Include=""System.Core"" />
    <Reference Include=""System.Xml.Linq"" />
    <Reference Include=""System.Data.DataSetExtensions"" />
    <Reference Include=""Microsoft.CSharp"" />
    <Reference Include=""System.Data"" />
    <Reference Include=""System.Xml"" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""Properties\AssemblyInfo.cs"" />
  </ItemGroup>
  <ItemGroup>
    {2}
  </ItemGroup>
  <ItemGroup>
    {3}
  </ItemGroup>
  <ItemGroup>
    {4}
  </ItemGroup>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
</Project>
", projectName, targetFrameworkVersion, contentItemGroup, compileItemGroup, linkItemGroup);
        }

        private static void WriteProjectFile(string path, string contents) {
            string fullPath = Path.Combine(ProjectFilesFolder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, contents);
        }

        private static void WriteAssemblyInfo(string assemblyName, string version, string author, string description) {
            WriteAssemblyInfo(assemblyName, version, author, description, null);
        }

        private static void WriteAssemblyInfo(string assemblyName, string version, string author, string description, string title) {
            WriteProjectFile(@"Properties\AssemblyInfo.cs", GetAssemblyInfoContent(assemblyName, version, author, description, title));
        }

        private static string GetAssemblyInfoContent(string assemblyName, string version, string author, string description, string title) {
            return String.Format(@"using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle(""{4}"")]
[assembly: AssemblyDescription(""{3}"")]
[assembly: AssemblyConfiguration("""")]
[assembly: AssemblyCompany(""{2}"")]
[assembly: AssemblyProduct(""{0}"")]
[assembly: AssemblyCopyright(""Copyright � NuGet"")]
[assembly: AssemblyTrademark("""")]
[assembly: AssemblyCulture("""")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion(""{1}"")]
[assembly: AssemblyFileVersion(""{1}"")]
", assemblyName, version, author, description, title);
        }

        private static void DeleteDirs() {
            DeleteDir(NoSpecsfolder);
            DeleteDir(OneSpecfolder);
            DeleteDir(TwoSpecsFolder);
            DeleteDir(SpecificFilesFolder);
            DeleteDir(OutputFolder);
            DeleteDir(ProjectFilesFolder);
        }

        private static void DeleteDir(string directory) {
            try {
                if (Directory.Exists(directory)) {
                    foreach (var file in Directory.GetFiles(directory)) {
                        try {
                            File.Delete(file);
                        }
                        catch (FileNotFoundException) {

                        }
                    }
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (DirectoryNotFoundException) {

            }
        }
    }
}