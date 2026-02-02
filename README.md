# SmartApartment Geocode API

This project is a serverless application that acts as a caching proxy for the Google Geocoding API. It is built with .NET 6, AWS Lambda, and AWS DynamoDB, following Clean Architecture principles for an enterprise-ready design.

The primary goal is to reduce latency and API costs by caching responses from the Google Geocoding API for 30 days.

## Key Features

- **Serverless API**: An AWS Lambda function that handles incoming requests via Amazon API Gateway.
- **30-Day Caching**: Responses are stored in a DynamoDB table with a Time-To-Live (TTL) of 30 days. Subsequent requests for the same address are served from the cache.
- **Clean Architecture**: The solution is structured into three distinct layers (`Core`, `Infrastructure`, `Api`) to ensure separation of concerns, testability, and maintainability.
- **Infrastructure as Code (IaC)**: All AWS resources (Lambda, DynamoDB table, API Gateway, IAM roles) are defined in the `template.yaml` file using the AWS Serverless Application Model (SAM).
- **Secure Configuration**: The Google API key is managed as a parameter in the SAM template and injected as an environment variable, avoiding hard-coded secrets.

## Architecture

- **`src/SmartApartment.Geocode.Core`**: Contains domain logic and interfaces. It has no external dependencies.
- **`src/SmartApartment.Geocode.Infrastructure`**: Implements the interfaces defined in `Core`. It contains the logic for interacting with external services like the Google API and DynamoDB.
- **`src/SmartApartment.Geocode.Api`**: The entry point of the application. It contains the Lambda function handler, dependency injection setup, and API routing logic.

## Prerequisites

To build, deploy, and test this application, you will need:

1.  **AWS Account** and configured credentials.
2.  **Google Geocoding API Key**.
3.  **AWS SAM CLI** - [Install the SAM CLI](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/serverless-sam-cli-install.html).
4.  **.NET 6 SDK** - [Install .NET 6](https://dotnet.microsoft.com/en-us/download/dotnet/6.0).
5.  **Docker** - [Install Docker](https://www.docker.com/products/docker-desktop/).

## Deployment

Deploy the application to your AWS account using the SAM CLI.

1.  **Build the application:**
    ```bash
    sam build
    ```

2.  **Deploy with the guided process:**
    ```bash
    sam deploy --guided
    ```

    You will be prompted for several parameters:
    - **Stack Name**: A unique name for your stack (e.g., `smart-apartment-geocode`).
    - **AWS Region**: The region to deploy to (e.g., `us-east-1`).
    - **Parameter GoogleApiKey**: **Paste your Google Geocoding API Key here.**
    - **Confirm changes before deploy**: `y`
    - **Allow SAM CLI IAM role creation**: `y`
    - **Save arguments to samconfig.toml**: `y`

After deployment, the API Gateway endpoint URL will be displayed in the outputs.

## How to Use

Invoke the API using a GET request with an `address` query parameter.

**Example using `curl`:**
```bash
curl "https://<your-api-id>.execute-api.<your-region>.amazonaws.com/Prod/Geocode?address=70+Vanderbilt+Ave,+New+York,+NY+10017,+United+States"
```

- The **first request** for an address will call the Google API and return a response with an `X-Cache: MISS` header.
- **Subsequent requests** for the same address within 30 days will be served from the DynamoDB cache and return a response with an `X-Cache: HIT` header.

## Local Development & Testing

You can run the API locally to test changes without deploying to AWS.

1.  **Start the local API:**
    ```bash
    sam local start-api
    ```

2.  **Invoke the local endpoint:**
    *Note: You will need to set the `GoogleApiKey` as a local environment variable for this to work.*
    ```bash
    curl "http://127.0.0.1:3000/Geocode?address=70+Vanderbilt+Ave,+New+York,+NY+10017,+United+States"
    ```

## Cleanup

To delete the application and all associated resources from your AWS account, run the following command, replacing `<stack-name>` with the name you provided during deployment.

```bash
sam delete --stack-name <stack-name>
```
