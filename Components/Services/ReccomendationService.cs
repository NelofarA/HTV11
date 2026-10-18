using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HTV11.Models;
using Microsoft.Extensions.Configuration;



namespace HTV11.Components.Services;

public sealed record GeneratedAttraction(
    string Name,
    string Neighborhood,
    string Summary,
    string WhyRecommended,
    string[] Interests,
    string AccessibilityNotes,
    string AgeNotes,
    string TransitNotes,
    string PotentialDrawbacks,
    string VisitorTips);
public sealed class AttractionRecommendationException(string message) : Exception(message);

	public sealed record AttractionRecommendation(
		GeneratedAttraction Attraction,
		int MatchScore);

public sealed class AttractionRecommendationService(
    HttpClient httpClient,
    IConfiguration configuration)
{
    private const string DefaultModel = "gemini-3.8-flash";
    private const int RecommendationCount = 6;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<AttractionRecommendation>> RecommendAsync(
        Preferences preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var accessibilityNeeds = preferences.AccessibilityNeeds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var interests = preferences.Interests
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AttractionRecommendationException(
                "Gemini recommendations aren’t configured yet. From the project folder, run `dotnet user-secrets set \"Gemini:ApiKey\" \"YOUR_GEMINI_API_KEY\"`, or set the `Gemini__ApiKey` environment variable. Google AI Studio offers a free API tier subject to its quotas.");
        }

        var model = configuration["Gemini:Model"] ?? DefaultModel;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"models/{Uri.EscapeDataString(model)}:generateContent");
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = JsonContent.Create(new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new
                    {
                        text = """
                        You are a careful Toronto local discovery assistant. Return valid JSON only, shaped as {"recommendations":[...]}.
                        Suggest six real, distinct Toronto-area places or public spaces, varying the recommendations on each request.
                        Favor lesser-known alternatives to the city's busiest tourist hotspots, but never claim current live availability, opening hours, admission prices, accessibility, or transit service as verified facts.
                        Give each place a concise factual-sounding summary and explain why it may fit the travel preferences. If unsure about a venue-specific fact, say it should be confirmed rather than guessing.
                        Do not invent a match score, official web address, or precise distance.
                        For accessibility, distinguish likely/general notes from guaranteed access; always advise confirming directly with the venue.
                        For potentialDrawbacks, honestly mention relevant tradeoffs such as limited food options, a long visit, stairs, outdoor exposure, or height/age restrictions. Only state venue-specific restrictions as confirmed when reliable; otherwise clearly say to verify with the venue. If no meaningful drawback is known, say details should be confirmed before visiting rather than inventing one.
                        For visitorTips, give one concise, practical preparation tip that follows from known or explicitly uncertain details, such as packing a lunch when food options may be limited. Do not present guesses as facts.
                        Each recommendation must have: name, neighborhood, summary (1-2 sentences), whyRecommended (one sentence), interests (array using only art, architecture, food, history, markets, nature, science, local-culture), accessibilityNotes, ageNotes, transitNotes, potentialDrawbacks (one or two concise sentences), visitorTips (one practical sentence).
                        Return no more than six recommendations. Do not include markdown. Make sure you prioritize and target lesser-known attractions as that is the purpose of the request and avoid repeating the same places in multiple requests.
                        """
                    }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new
                        {
                            text = $"""
                        Generate a fresh set of Toronto attraction recommendations.
                        Vary the mix from previous requests; request variation key: {Guid.NewGuid():N}.
                        Visitor interests: {FormatList(interests)}.
                        Additional filters: {FormatList(interests)}.
                        Age group: {preferences.AgeGroup ?? "not specified"}.
                        Accessibility needs: {FormatList(accessibilityNeeds)}.
                        Preferred travel mode: {preferences.TravelMode ?? "not specified"}.
                        Spending preference: {DescribeBudgetPreference(preferences.BudgetPreference)}.
                        Preferred setting: {DescribeSettingPreference(preferences.SettingPreference)}.
                        Only recommend places that plausibly fit the age and accessibility preferences; be explicit when details are uncertain.
                        Respect the spending, duration, setting, and pace preferences when choosing and explaining recommendations. Do not claim current prices or guarantee visit duration.
                        """
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.9,
                responseMimeType = "application/json"
            }
        }, options: JsonOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(response.StatusCode, model);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var geminiResponse = JsonSerializer.Deserialize<GeneratedGeminiResponse>(responseBody, JsonOptions)
            ?? throw new AttractionRecommendationException("Gemini returned an empty recommendation response.");

        var recommendationText = geminiResponse.Candidates
            .SelectMany(candidate => candidate.Content?.Parts ?? [])
            .Select(part => part.Text)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

        if (string.IsNullOrWhiteSpace(recommendationText))
        {
            throw new AttractionRecommendationException("Gemini did not return a valid recommendations payload.");
        }

        var result = JsonSerializer.Deserialize<GeneratedRecommendationResponse>(recommendationText, JsonOptions)
            ?? throw new AttractionRecommendationException("Gemini returned a malformed recommendation payload.");

        var recommendations = result.Recommendations
            .Where(attraction => attraction is not null)
            .Take(RecommendationCount)
            .Select(attraction => new AttractionRecommendation(
                attraction,
                CalculateMatchScore(attraction, interests, accessibilityNeeds, preferences)))
            .ToArray();

        return recommendations;
    }

    private static AttractionRecommendationException CreateApiException(HttpStatusCode statusCode, string model) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new("Gemini rejected the configured API key. Check your Gemini:ApiKey setting."),
            HttpStatusCode.TooManyRequests =>
                new("Gemini rate limit or free-tier quota reached. Wait a moment or check your Google AI Studio quota."),
            HttpStatusCode.BadRequest =>
                new("Gemini rejected the request. Check that the API key is enabled for the Generative Language API."),
            HttpStatusCode.NotFound =>
                new($"Gemini model '{model}' wasn’t found. Check the Gemini:Model setting."),
            _ when (int)statusCode >= 500 =>
                new("Gemini is temporarily unavailable. Please try again shortly."),
            _ =>
                new($"The Gemini recommendation request failed ({(int)statusCode}). Please try again.")
        };

    private static string FormatList(IEnumerable<string> values)
    {
        var items = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return items.Length == 0 ? "none specified" : string.Join(", ", items);
    }

    private static string DescribeBudgetPreference(string? preference) => preference switch
    {
        "free-low-cost" => "prefer free or lower-cost activities",
        "moderate" => "moderate spending is acceptable",
        "premium" => "higher-cost experiences are acceptable",
        _ => "no spending preference"
    };

    private static string DescribeSettingPreference(string? setting) => setting switch
    {
        "indoors" => "prefer indoor places",
        "outdoors" => "prefer outdoor places",
        "mix" => "prefer a mix of indoor and outdoor places",
        _ => "no setting preference"
    };

    private static int CalculateMatchScore(
        GeneratedAttraction attraction,
        IReadOnlyCollection<string> interests,
        IReadOnlyCollection<string> accessibilityNeeds,
        Preferences preferences)
    {
        var score = 40;

        score += attraction.Interests
            .Where(interest => !string.IsNullOrWhiteSpace(interest))
            .Count(interest => interests.Contains(interest, StringComparer.OrdinalIgnoreCase)) * 20;

        if (!string.IsNullOrWhiteSpace(preferences.AgeGroup)
            && !string.IsNullOrWhiteSpace(attraction.AgeNotes)
            && attraction.AgeNotes.Contains(preferences.AgeGroup, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        if (accessibilityNeeds.Count > 0
            && !string.IsNullOrWhiteSpace(attraction.AccessibilityNotes)
            && !attraction.AccessibilityNotes.Contains("not", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        if (!string.IsNullOrWhiteSpace(preferences.TravelMode)
            && !string.IsNullOrWhiteSpace(attraction.TransitNotes)
            && attraction.TransitNotes.Contains(preferences.TravelMode, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        return Math.Clamp(score, 0, 100);
    }

    private sealed class GeneratedRecommendationResponse
    {
        public GeneratedAttraction[] Recommendations { get; init; } = [];
    }

    private sealed class GeneratedGeminiResponse
    {
        public GeneratedGeminiCandidate[] Candidates { get; init; } = [];
    }

    private sealed class GeneratedGeminiCandidate
    {
        public GeneratedGeminiContent? Content { get; init; }
    }

    private sealed class GeneratedGeminiContent
    {
        public GeneratedGeminiPart[] Parts { get; init; } = [];
    }

    private sealed class GeneratedGeminiPart
    {
        public string? Text { get; init; }
    }
}