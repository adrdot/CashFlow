using Aspire.CashFlow.AppHost;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

CognitoLocalEnvironmentLoader.TryLoad();

var configuration = builder.Configuration;

var useCognitoLocal = IsEnabled(
    "CASHFLOW_COGNITO_ENABLED",
    configuration["CashFlow:CognitoEnabled"] ?? configuration["Parameters:cognito-enabled"]
);

var useLocalStack =
    IsEnabled("CASHFLOW_LOCALSTACK_ENABLED", configuration["CashFlow:LocalStackEnabled"])
    || !string.IsNullOrWhiteSpace(
        ResolveSetting(
            configuration,
            "SECRETS_MANAGER_SERVICE_URL",
            "Parameters:secrets-service-url"
        )
    )
    || !string.IsNullOrWhiteSpace(
        ResolveSetting(configuration, "KMS_SERVICE_URL", "Parameters:kms-service-url")
    );

var cognitoRegion = builder.AddParameter(
    "cognito-region",
    RequireSetting(
        configuration,
        "CASHFLOW_COGNITO_REGION",
        "Parameters:cognito-region",
        "Cognito:Region"
    )
);

var cognitoEnabledDefault =
    configuration["Parameters:cognito-enabled"] ?? (useCognitoLocal ? "true" : "false");

var cognitoEnabled = builder.AddParameter("cognito-enabled", cognitoEnabledDefault);

var cognitoServiceUrl = builder.AddParameter(
    "cognito-service-url",
    RequireSetting(
        configuration,
        "CASHFLOW_COGNITO_SERVICE_URL",
        "Parameters:cognito-service-url",
        "Cognito:ServiceUrl"
    )
);

var cognitoUserPoolId = builder.AddParameter(
    "cognito-user-pool-id",
    RequireSetting(
        configuration,
        "CASHFLOW_COGNITO_USER_POOL_ID",
        "Parameters:cognito-user-pool-id",
        "Cognito:UserPoolId"
    )
);

var cognitoClientId = builder.AddParameter(
    "cognito-client-id",
    RequireSetting(
        configuration,
        "CASHFLOW_COGNITO_CLIENT_ID",
        "Parameters:cognito-client-id",
        "Cognito:ClientId"
    )
);

var cognitoAuthenticationSource = builder.AddParameter(
    "cognito-authentication-source",
    RequireSetting(
        configuration,
        "CASHFLOW_COGNITO_AUTHENTICATION_SOURCE",
        "Parameters:cognito-authentication-source",
        "Cognito:AuthenticationSource"
    )
);

var secretsPrefix = builder.AddParameter(
    "secrets-prefix",
    RequireSetting(
        configuration,
        "SECRETS_PREFIX",
        "Parameters:secrets-prefix",
        "SecretsManager:Prefix"
    )
);

var secretsServiceUrl = builder.AddParameter(
    "secrets-service-url",
    RequireSetting(
        configuration,
        "SECRETS_MANAGER_SERVICE_URL",
        "Parameters:secrets-service-url",
        "SecretsManager:ServiceUrl"
    )
);

var kmsDefaultKeyId = builder.AddParameter(
    "kms-default-key-id",
    RequireSetting(
        configuration,
        "KMS_DEFAULT_KEY_ID",
        "Parameters:kms-default-key-id",
        "Kms:DefaultKeyId"
    )
);

var kmsServiceUrl = builder.AddParameter(
    "kms-service-url",
    RequireSetting(configuration, "KMS_SERVICE_URL", "Parameters:kms-service-url", "Kms:ServiceUrl")
);

var reportingDbConnection = RequireSetting(
    configuration,
    "ConnectionStrings__reporting-db",
    "ConnectionStrings:reporting-db"
);

var reportingRedisConfiguration = RequireSetting(
    configuration,
    "Reporting__Redis__Configuration",
    "Reporting:Redis:Configuration"
);

var reportingRedisEnabled = !string.Equals(
    ResolveSetting(configuration, "Reporting__Redis__Enabled", "Reporting:Redis:Enabled"),
    "false",
    StringComparison.OrdinalIgnoreCase
);

var eventStoreConnection = RequireSetting(
    configuration,
    "EventStore__ConnectionString",
    "EventStore:ConnectionString"
);

var snsTopicArn = ResolveSetting(configuration, "Messaging__SnsTopicArn", "Messaging:SnsTopicArn");

var sqsQueueUrl = ResolveSetting(configuration, "Messaging__SqsQueueUrl", "Messaging:SqsQueueUrl");

var dlqQueueUrl = ResolveSetting(configuration, "Messaging__DlqQueueUrl", "Messaging:DlqQueueUrl");

var awsServiceUrl = RequireSetting(
    configuration,
    "AWS__ServiceURL",
    "AWS:ServiceURL",
    "LOCALSTACK_SERVICE_URL"
);

var awsRegion = RequireSetting(configuration, "AWS__Region", "AWS:Region");

var awsAccessKey = RequireSetting(
    configuration,
    "AWS_ACCESS_KEY_ID",
    "AWS:AccessKey",
    "AWS__AccessKey"
);

var awsSecretKey = RequireSetting(
    configuration,
    "AWS_SECRET_ACCESS_KEY",
    "AWS:SecretKey",
    "AWS__SecretKey"
);

var awsLocalStackAccountId = RequireSetting(
    configuration,
    "AWS__LocalStackAccountId",
    "AWS:LocalStackAccountId"
);

var localAuthMfaCode = RequireSetting(
    configuration,
    "COGNITO_MFA_CODE",
    "LocalAuth:MfaCode",
    "DemoAccount:MfaCode"
);

var demoAccountEmail = RequireSetting(configuration, "COGNITO_USERNAME", "DemoAccount:Email");

var demoAccountPassword = RequireSetting(configuration, "COGNITO_PASSWORD", "DemoAccount:Password");

var demoAccountDescription = RequireSetting(
    configuration,
    "DEMO_ACCOUNT_DESCRIPTION",
    "DemoAccount:Description"
);

var cloudWatchLogGroupPrefix = RequireSetting(
    configuration,
    "CLOUDWATCH_LOG_GROUP_PREFIX",
    "CloudWatch:LogGroupPrefix"
);

var apiReplicas = ResolveApiReplicas(configuration);
var relayReplicas = ResolveRelayReplicas(configuration);
var reportingWorkerReplicas = ResolveReportingWorkerReplicas(configuration);

var authApiBuilder = builder.AddProject<Projects.CashFlow_Auth_Api>("auth-api");
var transactionsApiBuilder = builder
    .AddProject<Projects.CashFlow_Transactions_Api>("transactions-api")
    .WithReplicas(apiReplicas);
var transactionsRelayBuilder = builder
    .AddProject<Projects.CashFlow_Transactions_Relay>("transactions-relay")
    .WithReplicas(relayReplicas);
var reportingApiBuilder = builder
    .AddProject<Projects.CashFlow_Reporting_Api>("reporting-api")
    .WithReplicas(apiReplicas);
var reportingWorkerBuilder = builder
    .AddProject<Projects.CashFlow_Reporting_Worker>("reporting-worker")
    .WithReplicas(reportingWorkerReplicas);
var webBuilder = builder.AddProject<Projects.CashFlow_Web>("web");

var authApiHttpsEndpoint = authApiBuilder.GetEndpoint("https");

var webHttpsEndpoint = webBuilder.GetEndpoint("https");

var oauthRedirectUri = ReferenceExpression.Create($"{webHttpsEndpoint}/auth/callback");

static bool IsEnabled(string environmentVariableName, string? configurationValue)
{
    var env = Environment.GetEnvironmentVariable(environmentVariableName);
    if (!string.IsNullOrWhiteSpace(env))
    {
        return string.Equals(env, "true", StringComparison.OrdinalIgnoreCase);
    }

    return string.Equals(configurationValue, "true", StringComparison.OrdinalIgnoreCase);
}

static string ResolveSetting(
    IConfiguration configuration,
    string environmentVariableName,
    string? configurationKey,
    string? additionalConfigurationKey = null
)
{
    var env = Environment.GetEnvironmentVariable(environmentVariableName);
    if (!string.IsNullOrWhiteSpace(env))
    {
        return env;
    }

    if (!string.IsNullOrWhiteSpace(configurationKey))
    {
        var config = configuration[configurationKey];
        if (!string.IsNullOrWhiteSpace(config))
        {
            return config;
        }
    }

    if (!string.IsNullOrWhiteSpace(additionalConfigurationKey))
    {
        var config = configuration[additionalConfigurationKey];
        if (!string.IsNullOrWhiteSpace(config))
        {
            return config;
        }
    }

    return string.Empty;
}

static string RequireSetting(
    IConfiguration configuration,
    string environmentVariableName,
    params string[] configurationKeys
)
{
    var env = Environment.GetEnvironmentVariable(environmentVariableName);
    if (!string.IsNullOrWhiteSpace(env))
    {
        return env;
    }

    foreach (var key in configurationKeys)
    {
        var config = configuration[key];
        if (!string.IsNullOrWhiteSpace(config))
        {
            return config;
        }
    }

    throw new InvalidOperationException(
        $"Configuration value is required. Set environment variable '{environmentVariableName}' or one of: {string.Join(", ", configurationKeys)}."
    );
}

static int ResolveApiReplicas(IConfiguration configuration)
{
    var value = RequireSetting(configuration, "CASHFLOW_API_REPLICAS", "CashFlow:ApiReplicas");
    if (!int.TryParse(value, out var replicas) || replicas <= 0)
    {
        throw new InvalidOperationException("CashFlow:ApiReplicas must be a positive integer.");
    }

    return replicas;
}

static int ResolveRelayReplicas(IConfiguration configuration)
{
    var value = RequireSetting(configuration, "CASHFLOW_RELAY_REPLICAS", "CashFlow:RelayReplicas");
    if (!int.TryParse(value, out var replicas) || replicas <= 0)
    {
        throw new InvalidOperationException("CashFlow:RelayReplicas must be a positive integer.");
    }

    return replicas;
}

static int ResolveReportingWorkerReplicas(IConfiguration configuration)
{
    var value = RequireSetting(
        configuration,
        "CASHFLOW_REPORTING_WORKER_REPLICAS",
        "CashFlow:ReportingWorkerReplicas"
    );
    if (!int.TryParse(value, out var replicas) || replicas <= 0)
    {
        throw new InvalidOperationException(
            "CashFlow:ReportingWorkerReplicas must be a positive integer."
        );
    }

    return replicas;
}

static IResourceBuilder<ProjectResource> ConfigureCognito(
    IResourceBuilder<ProjectResource> project,
    bool useCognitoLocal,
    IResourceBuilder<ParameterResource> cognitoEnabled,
    IResourceBuilder<ParameterResource> cognitoRegion,
    IResourceBuilder<ParameterResource> cognitoServiceUrl,
    IResourceBuilder<ParameterResource> cognitoUserPoolId,
    IResourceBuilder<ParameterResource> cognitoClientId,
    IResourceBuilder<ParameterResource> cognitoAuthenticationSource,
    ReferenceExpression oauthRedirectUri,
    string awsAccessKey,
    string awsSecretKey,
    string localAuthMfaCode
)
{
    var configured = project
        .WithEnvironment("Cognito__Enabled", cognitoEnabled)
        .WithEnvironment("Cognito__Region", cognitoRegion)
        .WithEnvironment("Cognito__ServiceUrl", cognitoServiceUrl)
        .WithEnvironment("Cognito__UserPoolId", cognitoUserPoolId)
        .WithEnvironment("Cognito__ClientId", cognitoClientId)
        .WithEnvironment("Cognito__AuthenticationSource", cognitoAuthenticationSource)
        .WithEnvironment("Cognito__OAuth__Enabled", "true")
        .WithEnvironment("Cognito__OAuth__RedirectUri", oauthRedirectUri)
        .WithEnvironment("AWS_ACCESS_KEY_ID", awsAccessKey)
        .WithEnvironment("AWS_SECRET_ACCESS_KEY", awsSecretKey)
        .WithEnvironment("AWS_REGION", cognitoRegion)
        .WithEnvironment("AWS_DEFAULT_REGION", cognitoRegion);

    if (!useCognitoLocal)
    {
        return configured;
    }

    return configured
        .WithEnvironment("Cognito__Enabled", "true")
        .WithEnvironment("Cognito__RequireMfa", "true")
        .WithEnvironment("Cognito__Region", cognitoRegion)
        .WithEnvironment("Cognito__ServiceUrl", cognitoServiceUrl)
        .WithEnvironment("Cognito__UserPoolId", cognitoUserPoolId)
        .WithEnvironment("Cognito__ClientId", cognitoClientId)
        .WithEnvironment("Cognito__AuthenticationSource", cognitoAuthenticationSource)
        .WithEnvironment("LocalAuth__MfaCode", localAuthMfaCode);
}

static IResourceBuilder<ProjectResource> ConfigureTransactionsInfrastructure(
    IResourceBuilder<ProjectResource> project,
    IConfiguration configuration,
    string eventStoreConnectionString,
    string snsTopicArn,
    string sqsQueueUrl,
    string dlqQueueUrl,
    string awsServiceUrl,
    string awsRegion,
    string awsAccessKey,
    string awsSecretKey,
    string awsLocalStackAccountId,
    bool enableMessaging
)
{
    var messagingEnabled = enableMessaging && !string.IsNullOrWhiteSpace(snsTopicArn);

    return project
        .WithEnvironment("EventStore__ConnectionString", eventStoreConnectionString)
        .WithEnvironment(
            "EventStore__HttpEndpoint",
            RequireSetting(configuration, "EventStore__HttpEndpoint", "EventStore:HttpEndpoint")
        )
        .WithEnvironment("Messaging__SnsTopicArn", snsTopicArn)
        .WithEnvironment("Messaging__SqsQueueUrl", sqsQueueUrl)
        .WithEnvironment("Messaging__DlqQueueUrl", dlqQueueUrl)
        .WithEnvironment("Messaging__ServiceUrl", awsServiceUrl)
        .WithEnvironment("Messaging__Region", awsRegion)
        .WithEnvironment("Messaging__Enabled", messagingEnabled ? "true" : "false")
        .WithEnvironment("AWS__ServiceURL", awsServiceUrl)
        .WithEnvironment("AWS__Region", awsRegion)
        .WithEnvironment("AWS__AccessKey", awsAccessKey)
        .WithEnvironment("AWS__SecretKey", awsSecretKey)
        .WithEnvironment("AWS__LocalStackAccountId", awsLocalStackAccountId)
        .WithEnvironment("AWS_ACCESS_KEY_ID", awsAccessKey)
        .WithEnvironment("AWS_SECRET_ACCESS_KEY", awsSecretKey);
}

static IResourceBuilder<ProjectResource> ConfigureReportingInfrastructure(
    IResourceBuilder<ProjectResource> project,
    string reportingConnectionString,
    string sqsQueueUrl,
    string dlqQueueUrl,
    string awsServiceUrl,
    string awsRegion,
    string awsAccessKey,
    string awsSecretKey,
    string awsLocalStackAccountId,
    bool enableMessaging,
    bool enableRedis = false,
    string redisConfiguration = ""
)
{
    var configured = project
        .WithEnvironment("ConnectionStrings__reporting-db", reportingConnectionString)
        .WithEnvironment("Reporting__Redis__Enabled", enableRedis ? "true" : "false")
        .WithEnvironment("Reporting__Redis__Configuration", redisConfiguration)
        .WithEnvironment("Security__RateLimitingEnabled", "false");

    if (!enableMessaging)
    {
        return configured;
    }

    return configured
        .WithEnvironment("Messaging__SqsQueueUrl", sqsQueueUrl)
        .WithEnvironment("Messaging__DlqQueueUrl", dlqQueueUrl)
        .WithEnvironment("Messaging__ServiceUrl", awsServiceUrl)
        .WithEnvironment("Messaging__Region", awsRegion)
        .WithEnvironment("AWS__ServiceURL", awsServiceUrl)
        .WithEnvironment("AWS__Region", awsRegion)
        .WithEnvironment("AWS__AccessKey", awsAccessKey)
        .WithEnvironment("AWS__SecretKey", awsSecretKey)
        .WithEnvironment("AWS__LocalStackAccountId", awsLocalStackAccountId)
        .WithEnvironment("AWS_ACCESS_KEY_ID", awsAccessKey)
        .WithEnvironment("AWS_SECRET_ACCESS_KEY", awsSecretKey);
}

static IResourceBuilder<ProjectResource> ConfigureCloudWatchLogging(
    IResourceBuilder<ProjectResource> project,
    bool enabled,
    string serviceUrl,
    string region,
    string logGroupPrefix
)
{
    if (!enabled)
    {
        return project;
    }

    return project
        .WithEnvironment("CloudWatch__Enabled", "true")
        .WithEnvironment("CloudWatch__ServiceUrl", serviceUrl)
        .WithEnvironment("CloudWatch__Region", region)
        .WithEnvironment("CloudWatch__LogGroupPrefix", logGroupPrefix);
}

static IResourceBuilder<ProjectResource> ConfigureOtelCollector(
    IResourceBuilder<ProjectResource> project,
    IConfiguration configuration
)
{
    var collectorEndpoint = ResolveSetting(
        configuration,
        "CASHFLOW_OTEL_COLLECTOR_ENDPOINT",
        "CashFlow:OtelCollectorEndpoint"
    );
    if (string.IsNullOrWhiteSpace(collectorEndpoint))
    {
        return project;
    }

    return project
        .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", collectorEndpoint)
        .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
}

var authApi = ConfigureCognito(
        authApiBuilder,
        useCognitoLocal,
        cognitoEnabled,
        cognitoRegion,
        cognitoServiceUrl,
        cognitoUserPoolId,
        cognitoClientId,
        cognitoAuthenticationSource,
        oauthRedirectUri,
        awsAccessKey,
        awsSecretKey,
        localAuthMfaCode
    )
    .WithEnvironment("SecretsManager__Prefix", secretsPrefix)
    .WithEnvironment("SecretsManager__ServiceUrl", secretsServiceUrl)
    .WithEnvironment("SecretsManager__PreferConfiguration", useLocalStack ? "false" : "true")
    .WithEnvironment("Kms__DefaultKeyId", kmsDefaultKeyId)
    .WithEnvironment("Kms__ServiceUrl", kmsServiceUrl)
    .WithEnvironment("AWS__AccessKey", awsAccessKey)
    .WithEnvironment("AWS__SecretKey", awsSecretKey)
    .WithEnvironment("AWS__LocalStackAccountId", awsLocalStackAccountId)
    .WithEnvironment("AWS__ServiceURL", awsServiceUrl)
    .WithEnvironment("AWS__Region", awsRegion);

authApi = ConfigureCloudWatchLogging(
    authApi,
    useLocalStack,
    awsServiceUrl,
    awsRegion,
    cloudWatchLogGroupPrefix
);
authApi = ConfigureOtelCollector(authApi, configuration);

var transactionsApi = ConfigureTransactionsInfrastructure(
    ConfigureCognito(
        transactionsApiBuilder,
        useCognitoLocal,
        cognitoEnabled,
        cognitoRegion,
        cognitoServiceUrl,
        cognitoUserPoolId,
        cognitoClientId,
        cognitoAuthenticationSource,
        oauthRedirectUri,
        awsAccessKey,
        awsSecretKey,
        localAuthMfaCode
    ),
    configuration,
    eventStoreConnection,
    snsTopicArn,
    sqsQueueUrl,
    dlqQueueUrl,
    awsServiceUrl,
    awsRegion,
    awsAccessKey,
    awsSecretKey,
    awsLocalStackAccountId,
    enableMessaging: false
);

transactionsApi = ConfigureCloudWatchLogging(
    transactionsApi,
    useLocalStack,
    awsServiceUrl,
    awsRegion,
    cloudWatchLogGroupPrefix
);
transactionsApi = ConfigureOtelCollector(transactionsApi, configuration);

var transactionsRelay = ConfigureTransactionsInfrastructure(
    transactionsRelayBuilder,
    configuration,
    eventStoreConnection,
    snsTopicArn,
    sqsQueueUrl,
    dlqQueueUrl,
    awsServiceUrl,
    awsRegion,
    awsAccessKey,
    awsSecretKey,
    awsLocalStackAccountId,
    enableMessaging: true
);

transactionsRelay = ConfigureCloudWatchLogging(
    transactionsRelay,
    useLocalStack,
    awsServiceUrl,
    awsRegion,
    cloudWatchLogGroupPrefix
);
transactionsRelay = ConfigureOtelCollector(transactionsRelay, configuration);

var reportingApi = ConfigureReportingInfrastructure(
    ConfigureCognito(
        reportingApiBuilder,
        useCognitoLocal,
        cognitoEnabled,
        cognitoRegion,
        cognitoServiceUrl,
        cognitoUserPoolId,
        cognitoClientId,
        cognitoAuthenticationSource,
        oauthRedirectUri,
        awsAccessKey,
        awsSecretKey,
        localAuthMfaCode
    ),
    reportingDbConnection,
    sqsQueueUrl,
    dlqQueueUrl,
    awsServiceUrl,
    awsRegion,
    awsAccessKey,
    awsSecretKey,
    awsLocalStackAccountId,
    enableMessaging: false,
    enableRedis: reportingRedisEnabled,
    redisConfiguration: reportingRedisConfiguration
);

reportingApi = ConfigureCloudWatchLogging(
    reportingApi,
    useLocalStack,
    awsServiceUrl,
    awsRegion,
    cloudWatchLogGroupPrefix
);
reportingApi = ConfigureOtelCollector(reportingApi, configuration);

var reportingWorker = ConfigureReportingInfrastructure(
    reportingWorkerBuilder,
    reportingDbConnection,
    sqsQueueUrl,
    dlqQueueUrl,
    awsServiceUrl,
    awsRegion,
    awsAccessKey,
    awsSecretKey,
    awsLocalStackAccountId,
    enableMessaging: true,
    enableRedis: reportingRedisEnabled,
    redisConfiguration: reportingRedisConfiguration
);

reportingWorker = ConfigureCloudWatchLogging(
    reportingWorker,
    useLocalStack,
    awsServiceUrl,
    awsRegion,
    cloudWatchLogGroupPrefix
);
reportingWorker = ConfigureOtelCollector(reportingWorker, configuration);

ConfigureCognito(
        webBuilder,
        useCognitoLocal,
        cognitoEnabled,
        cognitoRegion,
        cognitoServiceUrl,
        cognitoUserPoolId,
        cognitoClientId,
        cognitoAuthenticationSource,
        oauthRedirectUri,
        awsAccessKey,
        awsSecretKey,
        localAuthMfaCode
    )
    .WithEnvironment("DemoAccount__Email", demoAccountEmail)
    .WithEnvironment("DemoAccount__Password", demoAccountPassword)
    .WithEnvironment("DemoAccount__MfaCode", localAuthMfaCode)
    .WithEnvironment("DemoAccount__Description", demoAccountDescription)
    .WithEnvironment("AuthApi__PublicBaseAddress", authApiHttpsEndpoint)
    .WithReference(authApi)
    .WithReference(transactionsApi)
    .WithReference(reportingApi)
    .WaitFor(reportingApi)
    .WaitFor(reportingWorker)
    .WaitFor(transactionsApi)
    .WaitFor(transactionsRelay)
    .WaitFor(authApi);

ConfigureOtelCollector(webBuilder, configuration);

if (useLocalStack)
{
    webBuilder = webBuilder
        .WithEnvironment("CloudWatch__Enabled", "true")
        .WithEnvironment("CloudWatch__ServiceUrl", awsServiceUrl)
        .WithEnvironment("CloudWatch__Region", awsRegion)
        .WithEnvironment("CloudWatch__LogGroupPrefix", cloudWatchLogGroupPrefix);
}

builder.Build().Run();
