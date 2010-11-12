﻿using System;
using System.Data.Services.Client;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace NuGet.Test {
    [TestClass]
    public class SmartDataServiceQueryTest {
        [TestMethod]
        public void GetEnumeratorExecutesBatchIfRequiresBatchTrue() {
            // Arrange
            var mockContext = new Mock<IDataServiceContext>();
            var mockQuery = new Mock<IDataServiceQuery>();
            mockQuery.Setup(m => m.RequiresBatch(It.IsAny<Expression>())).Returns(true);
            mockContext.Setup(m => m.CreateQuery<int>("Foo")).Returns(mockQuery.Object);
            mockContext.Setup(m => m.ExecuteBatch<int>(It.IsAny<DataServiceQuery>())).Returns(new[] { 1 }).Verifiable();
            var query = new SmartDataServiceQuery<int>(mockContext.Object, "Foo");

            // Act
            query.GetEnumerator();

            // Assert
            mockContext.VerifyAll();
        }

        [TestMethod]
        public void ProjectionTest() {
            // Arrange
            var mockContext = new Mock<IDataServiceContext>();
            var mockQuery = new Mock<IDataServiceQuery>();
            mockQuery.Setup(m => m.RequiresBatch(It.IsAny<Expression>())).Returns(false);
            mockQuery.Setup(m => m.GetEnumerator<int>(It.IsAny<Expression>())).Verifiable();
            mockContext.Setup(m => m.CreateQuery<string>("Foo")).Returns(mockQuery.Object);
            var query = from s in new SmartDataServiceQuery<string>(mockContext.Object, "Foo")
                        select Int32.Parse(s);

            // Act
            query.GetEnumerator();

            // Assert
            mockQuery.VerifyAll();
        }
    }
}
