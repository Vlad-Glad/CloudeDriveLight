public class Device
{
    private const int MaxNameLength = 100;

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; }


    public Device(
        Guid userId,
        string name)
    {
        ValidateGuid(userId, nameof(userId));
        ValidateName(name);

        Id = Guid.NewGuid();
        UserId = userId;
        Name = name;
    }


    public void Rename(string newName)
    {
        ValidateName(newName);

        Name = newName;
    }


    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Device name cannot be empty.",
                nameof(name));
        }

        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Device name cannot exceed {MaxNameLength} characters.",
                nameof(name));
        }
    }


    private static void ValidateGuid(
        Guid value,
        string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                $"{parameterName} cannot be an empty GUID.",
                parameterName);
        }
    }
}