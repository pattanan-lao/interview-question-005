# Build stage: restore and publish the API (and, transitively, the three
# libraries it references). Only the API project is published; Domain,
# Application and Infrastructure come along as its dependencies.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first so `dotnet restore` is cached independently of
# source changes. Test projects are deliberately excluded from the image.
COPY Example.QueueSystem.Domain/Example.QueueSystem.Domain.csproj Example.QueueSystem.Domain/
COPY Example.QueueSystem.Application/Example.QueueSystem.Application.csproj Example.QueueSystem.Application/
COPY Example.QueueSystem.Infrastructure/Example.QueueSystem.Infrastructure.csproj Example.QueueSystem.Infrastructure/
COPY Example.QueueSystem.Api/Example.QueueSystem.Api.csproj Example.QueueSystem.Api/
RUN dotnet restore Example.QueueSystem.Api/Example.QueueSystem.Api.csproj

COPY . .
RUN dotnet publish Example.QueueSystem.Api/Example.QueueSystem.Api.csproj \
    -c Release -o /app/publish --no-restore

# Runtime stage: aspnet image only, no SDK.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Listen on 8080 inside the container; nginx proxies to it by service name.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Example.QueueSystem.Api.dll"]
