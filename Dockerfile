FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY src/TransactionProcessor/TransactionProcessor.csproj ./src/TransactionProcessor/
RUN dotnet restore ./src/TransactionProcessor/TransactionProcessor.csproj

COPY src/ ./src/
RUN dotnet publish ./src/TransactionProcessor/TransactionProcessor.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "transaction-processor.dll"]
