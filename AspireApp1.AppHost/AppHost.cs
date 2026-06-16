using Aspire.Hosting.ApplicationModel;

using AspireApp1.AppHost;

using Microsoft.Extensions.Configuration;



var builder = DistributedApplication.CreateBuilder(args);



CognitoLocalEnvironmentLoader.TryLoad();

var configuration = builder.Configuration;

var useCognitoLocal = IsEnabled(
	"CASHFLOW_COGNITO_ENABLED",
	configuration["CashFlow:CognitoEnabled"] ?? configuration["Parameters:cognito-enabled"]);

var useLocalStack = IsEnabled(
	"CASHFLOW_LOCALSTACK_ENABLED",
	configuration["CashFlow:LocalStackEnabled"])
	|| !string.IsNullOrWhiteSpace(ResolveSetting(configuration, "SECRETS_MANAGER_SERVICE_URL", "Parameters:secrets-service-url"))
	|| !string.IsNullOrWhiteSpace(ResolveSetting(configuration, "KMS_SERVICE_URL", "Parameters:kms-service-url"));

var cognitoRegion = builder.AddParameter(
	"cognito-region",
	ResolveSetting(configuration, "CASHFLOW_COGNITO_REGION", "Parameters:cognito-region", "us-east-1"));

var cognitoEnabledDefault = configuration["Parameters:cognito-enabled"]
	?? (useCognitoLocal ? "true" : "false");

var cognitoEnabled = builder.AddParameter("cognito-enabled", cognitoEnabledDefault);

var cognitoServiceUrl = builder.AddParameter(
	"cognito-service-url",
	ResolveSetting(configuration, "CASHFLOW_COGNITO_SERVICE_URL", "Parameters:cognito-service-url"));

var cognitoUserPoolId = builder.AddParameter(
	"cognito-user-pool-id",
	ResolveSetting(configuration, "CASHFLOW_COGNITO_USER_POOL_ID", "Parameters:cognito-user-pool-id"));

var cognitoClientId = builder.AddParameter(
	"cognito-client-id",
	ResolveSetting(configuration, "CASHFLOW_COGNITO_CLIENT_ID", "Parameters:cognito-client-id"));

var cognitoAuthenticationSource = builder.AddParameter(
	"cognito-authentication-source",
	ResolveSetting(configuration, "CASHFLOW_COGNITO_AUTHENTICATION_SOURCE", "Parameters:cognito-authentication-source"));

var secretsPrefix = builder.AddParameter(
	"secrets-prefix",
	configuration["Parameters:secrets-prefix"] ?? "cashflow/");

var secretsServiceUrl = builder.AddParameter(
	"secrets-service-url",
	ResolveSetting(configuration, "SECRETS_MANAGER_SERVICE_URL", "Parameters:secrets-service-url"));

var kmsDefaultKeyId = builder.AddParameter(
	"kms-default-key-id",
	configuration["Parameters:kms-default-key-id"] ?? "alias/cashflow-default");

var kmsServiceUrl = builder.AddParameter(
	"kms-service-url",
	ResolveSetting(configuration, "KMS_SERVICE_URL", "Parameters:kms-service-url"));

var reportingDbConnection = ResolveSqlConnectionString(
	configuration,
	"ConnectionStrings__reporting-db",
	"ConnectionStrings:reporting-db",
	"Server=127.0.0.1,1433;Database=reporting-db;User Id=sa;Password=CashFlow@Dev123!;TrustServerCertificate=True;Encrypt=False");

var reportingRedisConfiguration = ResolveSetting(
	configuration,
	"Reporting__Redis__Configuration",
	"Reporting:Redis:Configuration",
	"localhost:6379");

var reportingRedisEnabled = !string.Equals(
	ResolveSetting(configuration, "Reporting__Redis__Enabled", "Reporting:Redis:Enabled", "true"),
	"false",
	StringComparison.OrdinalIgnoreCase);

var eventStoreConnection = ResolveSetting(
	configuration,
	"EventStore__ConnectionString",
	"EventStore:ConnectionString",
	"esdb://127.0.0.1:2113?tls=false");

var snsTopicArn = ResolveSetting(configuration, "Messaging__SnsTopicArn", "Messaging:SnsTopicArn");

var sqsQueueUrl = ResolveSetting(configuration, "Messaging__SqsQueueUrl", "Messaging:SqsQueueUrl");

var dlqQueueUrl = ResolveSetting(configuration, "Messaging__DlqQueueUrl", "Messaging:DlqQueueUrl");

var awsServiceUrl = ResolveSetting(
	configuration,
	"AWS__ServiceURL",
	"AWS:ServiceURL",
	ResolveSetting(configuration, "LOCALSTACK_SERVICE_URL", null, "http://localhost:4566"));

var awsRegion = ResolveSetting(configuration, "AWS__Region", "AWS:Region", "us-east-1");

var apiReplicas = ResolveApiReplicas(configuration);
var relayReplicas = ResolveRelayReplicas(configuration);
var reportingWorkerReplicas = ResolveReportingWorkerReplicas(configuration);

var authApiBuilder = builder.AddProject<Projects.CashFlow_Auth_Api>("auth-api");
var transactionsApiBuilder = builder.AddProject<Projects.CashFlow_Transactions_Api>("transactions-api")
	.WithReplicas(apiReplicas);
var transactionsRelayBuilder = builder.AddProject<Projects.CashFlow_Transactions_Relay>("transactions-relay")
	.WithReplicas(relayReplicas);
var reportingApiBuilder = builder.AddProject<Projects.CashFlow_Reporting_Api>("reporting-api")
	.WithReplicas(apiReplicas);
var reportingWorkerBuilder = builder.AddProject<Projects.CashFlow_Reporting_Worker>("reporting-worker")
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
	string fallback = "")
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

	return fallback;
}

static string ResolveSqlConnectionString(
	IConfiguration configuration,
	string environmentVariableName,
	string configurationKey,
	string fallback)
{
	var value = ResolveSetting(configuration, environmentVariableName, configurationKey, fallback);
	return string.IsNullOrWhiteSpace(value) || !value.Contains("Server=", StringComparison.OrdinalIgnoreCase)
		? fallback
		: value;
}

static int ResolveApiReplicas(IConfiguration configuration)
{
	var value = ResolveSetting(configuration, "CASHFLOW_API_REPLICAS", "CashFlow:ApiReplicas", "1");
	return int.TryParse(value, out var replicas) && replicas > 0 ? replicas : 1;
}

static int ResolveRelayReplicas(IConfiguration configuration)
{
	var value = ResolveSetting(configuration, "CASHFLOW_RELAY_REPLICAS", "CashFlow:RelayReplicas", "3");
	return int.TryParse(value, out var replicas) && replicas > 0 ? replicas : 3;
}

static int ResolveReportingWorkerReplicas(IConfiguration configuration)
{
	var value = ResolveSetting(configuration, "CASHFLOW_REPORTING_WORKER_REPLICAS", "CashFlow:ReportingWorkerReplicas", "3");
	return int.TryParse(value, out var replicas) && replicas > 0 ? replicas : 3;
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

	ReferenceExpression oauthRedirectUri)

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

		.WithEnvironment("AWS_ACCESS_KEY_ID", "test")

		.WithEnvironment("AWS_SECRET_ACCESS_KEY", "test")

		.WithEnvironment("AWS_REGION", cognitoRegion)

		.WithEnvironment("AWS_DEFAULT_REGION", cognitoRegion);



	if (!useCognitoLocal)

	{

		return configured;

	}



	return configured

		.WithEnvironment("Cognito__Enabled", "true")

		.WithEnvironment("Cognito__RequireMfa", "true")

		.WithEnvironment("Cognito__Region", Environment.GetEnvironmentVariable("CASHFLOW_COGNITO_REGION") ?? "us-east-1")

		.WithEnvironment("Cognito__ServiceUrl", Environment.GetEnvironmentVariable("CASHFLOW_COGNITO_SERVICE_URL") ?? "")

		.WithEnvironment("Cognito__UserPoolId", Environment.GetEnvironmentVariable("CASHFLOW_COGNITO_USER_POOL_ID") ?? "")

		.WithEnvironment("Cognito__ClientId", Environment.GetEnvironmentVariable("CASHFLOW_COGNITO_CLIENT_ID") ?? "")

		.WithEnvironment("Cognito__AuthenticationSource", Environment.GetEnvironmentVariable("CASHFLOW_COGNITO_AUTHENTICATION_SOURCE") ?? "CognitoLocal")

		.WithEnvironment("LocalAuth__MfaCode", Environment.GetEnvironmentVariable("COGNITO_MFA_CODE") ?? "123456");

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

	bool enableMessaging)

{

	var messagingEnabled = enableMessaging && !string.IsNullOrWhiteSpace(snsTopicArn);

	return project

		.WithEnvironment("EventStore__ConnectionString", eventStoreConnectionString)
		.WithEnvironment("EventStore__HttpEndpoint", ResolveSetting(configuration, "EventStore__HttpEndpoint", "EventStore:HttpEndpoint", "http://127.0.0.1:2113"))

		.WithEnvironment("Messaging__SnsTopicArn", snsTopicArn)

		.WithEnvironment("Messaging__SqsQueueUrl", sqsQueueUrl)

		.WithEnvironment("Messaging__DlqQueueUrl", dlqQueueUrl)

		.WithEnvironment("Messaging__ServiceUrl", awsServiceUrl)

		.WithEnvironment("Messaging__Region", awsRegion)

		.WithEnvironment("Messaging__Enabled", messagingEnabled ? "true" : "false")

		.WithEnvironment("AWS__ServiceURL", awsServiceUrl)

		.WithEnvironment("AWS__Region", awsRegion)

		.WithEnvironment("AWS_ACCESS_KEY_ID", "test")

		.WithEnvironment("AWS_SECRET_ACCESS_KEY", "test");

}

static IResourceBuilder<ProjectResource> ConfigureCloudWatchLogging(
	IResourceBuilder<ProjectResource> project,
	bool enabled,
	string serviceUrl,
	string region,
	string logGroupPrefix = "/cashflow")
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

static IResourceBuilder<ProjectResource> ConfigureReportingInfrastructure(
	IResourceBuilder<ProjectResource> project,
	string reportingConnectionString,
	string sqsQueueUrl,
	string dlqQueueUrl,
	string awsServiceUrl,
	string awsRegion,
	bool enableMessaging,
	bool enableRedis = false,
	string redisConfiguration = "localhost:6379")
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
		.WithEnvironment("AWS_ACCESS_KEY_ID", "test")
		.WithEnvironment("AWS_SECRET_ACCESS_KEY", "test");
}

static IResourceBuilder<ProjectResource> ConfigureOtelCollector(
	IResourceBuilder<ProjectResource> project,
	IConfiguration configuration)
{
	var collectorEndpoint = ResolveSetting(
		configuration,
		"CASHFLOW_OTEL_COLLECTOR_ENDPOINT",
		"CashFlow:OtelCollectorEndpoint");
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

	oauthRedirectUri)

	.WithEnvironment("SecretsManager__Prefix", secretsPrefix)

	.WithEnvironment("SecretsManager__ServiceUrl", secretsServiceUrl)

	.WithEnvironment("SecretsManager__PreferConfiguration", useLocalStack ? "false" : "true")

	.WithEnvironment("Kms__DefaultKeyId", kmsDefaultKeyId)

	.WithEnvironment("Kms__ServiceUrl", kmsServiceUrl);

authApi = ConfigureCloudWatchLogging(authApi, useLocalStack, awsServiceUrl, awsRegion);
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

		oauthRedirectUri),

	configuration,

	eventStoreConnection,

	snsTopicArn,

	sqsQueueUrl,

	dlqQueueUrl,

	awsServiceUrl,

	awsRegion,

	enableMessaging: false);

transactionsApi = ConfigureCloudWatchLogging(transactionsApi, useLocalStack, awsServiceUrl, awsRegion);
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

	enableMessaging: true);

transactionsRelay = ConfigureCloudWatchLogging(transactionsRelay, useLocalStack, awsServiceUrl, awsRegion);
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
		oauthRedirectUri),
	reportingDbConnection,
	sqsQueueUrl,
	dlqQueueUrl,
	awsServiceUrl,
	awsRegion,
	enableMessaging: false,
	enableRedis: reportingRedisEnabled,
	redisConfiguration: reportingRedisConfiguration);

reportingApi = ConfigureCloudWatchLogging(reportingApi, useLocalStack, awsServiceUrl, awsRegion);
reportingApi = ConfigureOtelCollector(reportingApi, configuration);



var reportingWorker = ConfigureReportingInfrastructure(
	reportingWorkerBuilder,
	reportingDbConnection,
	sqsQueueUrl,
	dlqQueueUrl,
	awsServiceUrl,
	awsRegion,
	enableMessaging: true,
	enableRedis: reportingRedisEnabled,
	redisConfiguration: reportingRedisConfiguration);

reportingWorker = ConfigureCloudWatchLogging(reportingWorker, useLocalStack, awsServiceUrl, awsRegion);
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

	oauthRedirectUri)

	.WithEnvironment("DemoAccount__Email", useCognitoLocal

		? (Environment.GetEnvironmentVariable("COGNITO_USERNAME") ?? "admin@cashflow.docker")

		: "admin@cashflow.local")

	.WithEnvironment("DemoAccount__Password", useCognitoLocal

		? (Environment.GetEnvironmentVariable("COGNITO_PASSWORD") ?? "Pass@word1")

		: "Pass@word1")

	.WithEnvironment("DemoAccount__MfaCode", Environment.GetEnvironmentVariable("COGNITO_MFA_CODE") ?? "123456")

	.WithEnvironment("DemoAccount__Description", useCognitoLocal

		? "Cognito Local demo account"

		: "In-memory demo account (MFA code 123456)")

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
		.WithEnvironment("CloudWatch__LogGroupPrefix", "/cashflow");
}



builder.Build().Run();


