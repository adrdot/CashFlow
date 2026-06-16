param(
    [string]$Endpoint = "http://localhost:4566",
    [string]$Region = "us-east-1",
    [string]$QueueName = "cashflow-transaction-recorded",
    [string]$DlqName = "cashflow-transaction-recorded-dlq",
    [int]$MaxMessages = 10
)

$ErrorActionPreference = "Stop"

function Invoke-AwsCli {
    param([string[]]$Arguments)

    if (Get-Command aws -ErrorAction SilentlyContinue) {
        aws @Arguments --endpoint-url $Endpoint --region $Region
        if ($LASTEXITCODE -ne 0) {
            throw "AWS CLI failed."
        }
        return
    }

    docker run --rm `
        --network localstack_default `
        -e AWS_ACCESS_KEY_ID=test `
        -e AWS_SECRET_ACCESS_KEY=test `
        -e AWS_DEFAULT_REGION=$Region `
        -e AWS_PAGER= `
        amazon/aws-cli @Arguments --endpoint-url http://cashflow-localstack:4566 --region $Region

    if ($LASTEXITCODE -ne 0) {
        throw "AWS CLI (docker) failed."
    }
}

$queueUrl = aws sqs get-queue-url --queue-name $QueueName --endpoint-url $Endpoint --region $Region --query QueueUrl --output text 2>$null
$dlqUrl = aws sqs get-queue-url --queue-name $DlqName --endpoint-url $Endpoint --region $Region --query QueueUrl --output text 2>$null

if (-not $queueUrl -or -not $dlqUrl) {
    throw "Could not resolve queue URLs. Ensure LocalStack messaging is provisioned."
}

Write-Host "Replaying up to $MaxMessages messages from DLQ to main queue..." -ForegroundColor Cyan

$messages = Invoke-AwsCli @(
    "sqs", "receive-message",
    "--queue-url", $dlqUrl,
    "--max-number-of-messages", "$MaxMessages",
    "--wait-time-seconds", "1",
    "--visibility-timeout", "30",
    "--attribute-names", "All"
)

if ([string]::IsNullOrWhiteSpace($messages)) {
    Write-Host "No messages in DLQ." -ForegroundColor Yellow
    return
}

$parsed = $messages | ConvertFrom-Json
if (-not $parsed.Messages) {
    Write-Host "No messages in DLQ." -ForegroundColor Yellow
    return
}

foreach ($message in $parsed.Messages) {
    Invoke-AwsCli @(
        "sqs", "send-message",
        "--queue-url", $queueUrl,
        "--message-body", $message.Body
    ) | Out-Null

    Invoke-AwsCli @(
        "sqs", "delete-message",
        "--queue-url", $dlqUrl,
        "--receipt-handle", $message.ReceiptHandle
    ) | Out-Null

    Write-Host "Replayed message $($message.MessageId)" -ForegroundColor Green
}
