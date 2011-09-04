﻿using Xunit;

namespace NuGet.Test {
    
    public class PackageIdValidatorTest {
        [Fact]
        public void ValidatePackageIdInvalidIdThrows() {
            // Arrange
            string packageId = "  Invalid  . Woo   .";

            // Act & Assert
            ExceptionAssert.ThrowsArgumentException(() => PackageIdValidator.ValidatePackageId(packageId), "The package ID '  Invalid  . Woo   .' contains invalid characters. Examples of valid package IDs include 'MyPackage' and 'MyPackage.Sample'.");
        }

        [Fact]
        public void EmptyIsNotValid() {
            // Arrange
            string packageId = "";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void NullThrowsException() {
            // Arrange
            string packageId = null;

            // Act & Assert
            ExceptionAssert.ThrowsArgNull(() => PackageIdValidator.IsValidPackageId(packageId), "packageId");
        }

        [Fact]
        public void AlphaNumericIsValid() {
            // Arrange
            string packageId = "42This1Is4You";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void MultipleDotSeparatorsAllowed() {
            // Arrange
            string packageId = "I.Like.Writing.Unit.Tests";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void NumbersAndWordsDotSeparatedAllowd() {
            // Arrange
            string packageId = "1.2.3.4.Uno.Dos.Tres.Cuatro";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void UnderscoreDotAndDashSeparatorsAreValid() {
            // Arrange
            string packageId = "Nu_Get.Core-IsCool";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void NonAlphaNumericUnderscoreDotDashIsInvalid() {
            // Arrange
            string packageId = "ILike*Asterisks";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ConsecutiveSeparatorsNotAllowed() {
            // Arrange
            string packageId = "I_.Like.-Separators";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void StartingWithSeparatorsNotAllowed() {
            // Arrange
            string packageId = "-StartWithSeparator";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void EndingWithSeparatorsNotAllowed() {
            // Arrange
            string packageId = "StartWithSeparator.";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.False(isValid);
        }
    }
}
