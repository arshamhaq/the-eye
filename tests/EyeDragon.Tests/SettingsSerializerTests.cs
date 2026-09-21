using EyeDragon.Core;

namespace EyeDragon.Tests;

public sealed class SettingsSerializerTests
{
    [Fact]
    public void SettingsRoundTrip()
    {
        var source = new AppSettings
        {
            WorkDurationMinutes = 35,
            MandatoryRestMinutes = 4,
            PetScale = 1.35,
            StrictModeEnabled = false,
            SelectedPet = "PurpleDragon"
        };

        var restored = SettingsSerializer.Deserialize(SettingsSerializer.Serialize(source));

        Assert.Equal(35, restored.WorkDurationMinutes);
        Assert.Equal(4, restored.MandatoryRestMinutes);
        Assert.Equal(1.35, restored.PetScale);
        Assert.False(restored.StrictModeEnabled);
        Assert.Equal("PurpleDragon", restored.SelectedPet);
    }

    [Fact]
    public void InvalidSettingsAreNormalized()
    {
        const string json = """
            { "workDurationMinutes": -10, "petScale": 99, "volume": -2 }
            """;

        var restored = SettingsSerializer.Deserialize(json);

        Assert.Equal(1, restored.WorkDurationMinutes);
        Assert.Equal(2.0, restored.PetScale);
        Assert.Equal(0, restored.Volume);
    }
}
