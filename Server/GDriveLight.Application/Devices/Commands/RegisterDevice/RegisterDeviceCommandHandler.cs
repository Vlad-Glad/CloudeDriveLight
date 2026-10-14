using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using GDriveLight.Domain.Models;
using MediatR;

namespace GDriveLight.Application.Devices.Commands.RegisterDevice;

public class RegisterDeviceCommandHandler : IRequestHandler<RegisterDeviceCommand, Result<Guid>>
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterDeviceCommandHandler(
        IDeviceRepository deviceRepository,
        IUnitOfWork unitOfWork)
    {
        _deviceRepository = deviceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(RegisterDeviceCommand request, CancellationToken cancellationToken)
    {
        var existing = await _deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken);
        if (existing is not null)
        {
            if (existing.UserId != request.UserId)
            {
                return Result<Guid>.Failure("This device is already registered to another user.");
            }

            if (existing.Name != request.Name)
            {
                existing.Rename(request.Name);
                _deviceRepository.Update(existing);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result<Guid>.Success(existing.Id);
        }

        var device = new Device(request.UserId, request.Name, request.DeviceId);
        await _deviceRepository.AddAsync(device, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(device.Id);
    }
}
