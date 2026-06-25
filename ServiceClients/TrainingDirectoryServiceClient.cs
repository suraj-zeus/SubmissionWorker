using SubmissionProcessor.Worker.Dtos;
using static System.Net.HttpStatusCode;

using System.Net;          
using System.Net.Http.Json;

namespace SubmissionProcessor.Worker.ServiceClients;


public class TrainingDirectoryServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TrainingDirectoryServiceClient> _logger;
    

    public TrainingDirectoryServiceClient(
        HttpClient httpClient,
        ILogger<TrainingDirectoryServiceClient> logger
    )
    {
        _logger = logger;
        _httpClient = httpClient;
    }



    public async Task<TraineeProfileDto?> GetTraineeByIdAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var trainee = await _httpClient.GetFromJsonAsync<TraineeProfileDto>($"api/directory/trainees/{id}", cancellationToken);
            return trainee;
        }
        catch (HttpRequestException ex) when (ex.StatusCode ==  HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Trainee with ID {Id} was not found.", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trainee with ID {Id}.", id);
            throw;
        }

    }
}