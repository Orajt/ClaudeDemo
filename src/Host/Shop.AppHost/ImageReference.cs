namespace Shop.AppHost;

/// <summary>Rozbiórka napisu <c>repozytorium:tag</c> na części wymagane przez Aspire (<c>AddContainer</c> + <c>WithImageTag</c>).</summary>
public readonly record struct ImageReference(string Repository, string Tag)
{
    public static ImageReference Parse(string image)
    {
        var separatorIndex = image.LastIndexOf(':');
        if (separatorIndex < 0)
            throw new ArgumentException($"Obraz '{image}' musi mieć tag (format repozytorium:tag).", nameof(image));

        return new ImageReference(image[..separatorIndex], image[(separatorIndex + 1)..]);
    }
}
