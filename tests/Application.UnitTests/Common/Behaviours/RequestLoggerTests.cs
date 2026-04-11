using backend.Application.Common.Behaviours;
using backend.Application.Common.Interfaces;
using backend.Application.ScoringScales.Commands.CreateScoringScale;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace backend.Application.UnitTests.Common.Behaviours;

public class RequestLoggerTests
{
    private Mock<ILogger<CreateScoringScaleCommand>> _logger = null!;
    private Mock<IUser> _user = null!;
    private Mock<IIdentityService> _identityService = null!;

    [SetUp]
    public void Setup()
    {
        _logger = new Mock<ILogger<CreateScoringScaleCommand>>();
        _user = new Mock<IUser>();
        _identityService = new Mock<IIdentityService>();
    }

    [Test]
    public async Task ShouldCallGetUserNameAsyncOnceIfAuthenticated()
    {
        _user.Setup(x => x.Id).Returns(Guid.NewGuid().ToString());

        var requestLogger = new LoggingBehaviour<CreateScoringScaleCommand>(
            _logger.Object, _user.Object, _identityService.Object);

        await requestLogger.Process(
            new CreateScoringScaleCommand { TestId = 1, Name = "Scale", FormulaExpression = "[Q1]" },
            new CancellationToken());

        _identityService.Verify(i => i.GetUserNameAsync(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task ShouldNotCallGetUserNameAsyncOnceIfUnauthenticated()
    {
        var requestLogger = new LoggingBehaviour<CreateScoringScaleCommand>(
            _logger.Object, _user.Object, _identityService.Object);

        await requestLogger.Process(
            new CreateScoringScaleCommand { TestId = 1, Name = "Scale", FormulaExpression = "[Q1]" },
            new CancellationToken());

        _identityService.Verify(i => i.GetUserNameAsync(It.IsAny<string>()), Times.Never);
    }
}
