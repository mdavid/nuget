namespace NuGet.Test {
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UriHelperTest {
        [TestMethod]
        public void CreatePartUriCreatesUriFromPath() {
            // Act
            Uri uri = UriUtility.CreatePartUri(@"a\b\c.txt");

            // Assert
            Assert.AreEqual(new Uri("/a/b/c.txt", UriKind.Relative), uri); 
        }

        [TestMethod]
        public void GetPathFromUri() {
            // Arrange
            Uri uri = new Uri("/a/b.txt", UriKind.Relative);

            // Act
            string path = UriUtility.GetPath(uri);

            // Assert
            Assert.AreEqual(@"a\b.txt", path);
        }
    }
}
