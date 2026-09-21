using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using EyeDragon.Core;

namespace EyeDragon.Services;

public sealed class PetService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LogService _log;

    public PetService(LogService log)
    {
        _log = log;
    }

    public PetDefinition Load(string petId)
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "Pets", petId, "pet.json");
        try
        {
            var definition = JsonSerializer.Deserialize<PetDefinition>(File.ReadAllText(manifestPath), JsonOptions)
                ?? throw new InvalidDataException("The pet manifest is empty.");
            if (string.IsNullOrWhiteSpace(definition.Id) || definition.Animations.Count == 0)
            {
                throw new InvalidDataException("The pet manifest is missing required fields.");
            }

            return definition;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            _log.Error($"Could not load pet '{petId}', using fallback", ex);
            return CreateFallback();
        }
    }

    public BitmapImage? LoadFrame(PetDefinition pet, string animation, int frameIndex = 0)
    {
        try
        {
            if (!pet.Animations.TryGetValue(animation, out var sequence) || sequence.Frames.Count == 0)
            {
                sequence = pet.Animations.GetValueOrDefault("idle");
            }

            if (sequence is null || sequence.Frames.Count == 0)
            {
                return null;
            }

            var relativePath = sequence.Frames[Math.Abs(frameIndex) % sequence.Frames.Count]
                .Replace('/', Path.DirectorySeparatorChar);
            var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Pets", pet.Id, relativePath));
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Pets", pet.Id)) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Pet frame path escapes the pet package.");
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            _log.Error($"Could not load animation '{animation}' for pet '{pet.Id}'", ex);
            return null;
        }
    }

    private static PetDefinition CreateFallback() => new()
    {
        Id = "PurpleDragon",
        Name = "Purple Dragon",
        Animations = new Dictionary<string, PetAnimation>(StringComparer.OrdinalIgnoreCase)
        {
            ["idle"] = new PetAnimation { Frames = ["Assets/idle.png"] }
        }
    };
}
