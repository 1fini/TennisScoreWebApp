nswag openapi2csclient \
    /input:"TennisScoreWebApp/ApiDefinitions/swagger.json" \
    /output:"TennisScoreWebApp/Services/TennisApiClient.cs" \
    /namespace:TennisScoreWebApp.Infrastructure.ExternalServices.TennisScoreApi \
    /GenerateClientClasses:true \
    /GenerateDtoTypes:true \
    /GenerateClientInterfaces:true \
    /ClassName:TennisApiClient \
    /InjectHttpClient:true