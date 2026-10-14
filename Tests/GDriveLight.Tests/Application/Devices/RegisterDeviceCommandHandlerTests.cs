using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Devices.Commands.RegisterDevice;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Devices;

public class RegisterDeviceCommandHandlerTests
{
    private readonly Mock<IDeviceRepository> _deviceRepository = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);

    [Fact]
    public async Task Handle_WhenDeviceDoesNotExist_CreatesDeviceAndSaves()
    {
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deviceName = "WorkStation";

        _deviceRepository.Setup(r => r.GetByIdAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Device?)null);

        _deviceRepository.Setup(r => r.AddAsync(It.Is<Device>(d => d.Id == deviceId && d.UserId == userId && d.Name == deviceName), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new RegisterDeviceCommandHandler(_deviceRepository.Object, _unitOfWork.Object);
        var result = await handler.Handle(new RegisterDeviceCommand(deviceId, userId, deviceName), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(deviceId, result.Value);
        _deviceRepository.Verify(r => r.AddAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDeviceExistsForSameUser_ReturnsExistingWithoutAdding()
    {
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deviceName = "WorkStation";
        var existingDevice = new Device(userId, deviceName, deviceId);

        _deviceRepository.Setup(r => r.GetByIdAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDevice);

        var handler = new RegisterDeviceCommandHandler(_deviceRepository.Object, _unitOfWork.Object);
        var result = await handler.Handle(new RegisterDeviceCommand(deviceId, userId, deviceName), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(deviceId, result.Value);
        _deviceRepository.Verify(r => r.AddAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDeviceExistsForSameUserAndNameChanged_UpdatesNameAndSaves()
    {
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingDevice = new Device(userId, "OldName", deviceId);

        _deviceRepository.Setup(r => r.GetByIdAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDevice);

        _deviceRepository.Setup(r => r.Update(existingDevice));

        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new RegisterDeviceCommandHandler(_deviceRepository.Object, _unitOfWork.Object);
        var result = await handler.Handle(new RegisterDeviceCommand(deviceId, userId, "NewName"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(deviceId, result.Value);
        Assert.Equal("NewName", existingDevice.Name);
        _deviceRepository.Verify(r => r.Update(existingDevice), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDeviceExistsForDifferentUser_ReturnsFailure()
    {
        var deviceId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var existingDevice = new Device(ownerUserId, "WorkStation", deviceId);

        _deviceRepository.Setup(r => r.GetByIdAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDevice);

        var handler = new RegisterDeviceCommandHandler(_deviceRepository.Object, _unitOfWork.Object);
        var result = await handler.Handle(new RegisterDeviceCommand(deviceId, requestingUserId, "WorkStation"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("another user", result.Error);
        _deviceRepository.Verify(r => r.AddAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
