using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;

namespace postech.Users.Api.Application.Services;

public class CognitoAuthService : ICognitoAuthService
{
    private readonly IAmazonCognitoIdentityProvider _cognitoClient;
    private readonly string _userPoolId;
    private readonly string _clientId;
    private readonly ILogger<CognitoAuthService> _logger;

    public CognitoAuthService(IConfiguration configuration, ILogger<CognitoAuthService> logger)
    {
        _logger = logger;

        var region = configuration["CognitoSettings:Region"]
                     ?? throw new InvalidOperationException("CognitoSettings:Region is not configured");

        _userPoolId = configuration["CognitoSettings:UserPoolId"]
                      ?? throw new InvalidOperationException("CognitoSettings:UserPoolId is not configured");

        _clientId = configuration["CognitoSettings:ClientId"]
                    ?? throw new InvalidOperationException("CognitoSettings:ClientId is not configured");

        _cognitoClient = new AmazonCognitoIdentityProviderClient(RegionEndpoint.GetBySystemName(region));
    }

    public async Task RegisterAsync(string email, string password, string name, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering user {Email} in Cognito", email);

        var signUpRequest = new SignUpRequest
        {
            ClientId = _clientId,
            Username = email,
            Password = password,
            UserAttributes = new List<AttributeType>
            {
                new AttributeType { Name = "email", Value = email },
                new AttributeType { Name = "name",  Value = name  }
            }
        };

        await _cognitoClient.SignUpAsync(signUpRequest, cancellationToken);

        // Auto-confirm the user — no email verification step required
        var confirmRequest = new AdminConfirmSignUpRequest
        {
            UserPoolId = _userPoolId,
            Username = email
        };

        await _cognitoClient.AdminConfirmSignUpAsync(confirmRequest, cancellationToken);

        _logger.LogInformation("User {Email} registered and confirmed in Cognito", email);
    }

    public async Task<string> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Authenticating user {Email} via Cognito", email);

        var authRequest = new InitiateAuthRequest
        {
            AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
            ClientId = _clientId,
            AuthParameters = new Dictionary<string, string>
            {
                { "USERNAME", email },
                { "PASSWORD", password }
            }
        };

        var response = await _cognitoClient.InitiateAuthAsync(authRequest, cancellationToken);

        _logger.LogInformation("User {Email} authenticated successfully via Cognito", email);

        // IdToken contains the user's identity claims (email, name, cognito:groups etc.)
        return response.AuthenticationResult.IdToken;
    }
}