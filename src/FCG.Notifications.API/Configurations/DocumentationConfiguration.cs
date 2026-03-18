using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace FCG.Notifications.API.Configurations;

public static class DocumentationConfiguration
{
    extension(IServiceCollection services)
	{
		public void AddDocumentation()
		{
			// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
			services.AddOpenApi(options =>
			{
				options.AddDocumentTransformer<InfoDocumentationTransformer>();
				options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
			});
		}
	}

	extension(WebApplication app)
	{
		public void UseDocumentation()
		{
			app.MapOpenApi();

			app.UseSwaggerUI(options =>
			{
				options.SwaggerEndpoint("/openapi/v1.json", "FCG Notifications API v1");
				options.DocumentTitle = "FCG Notifications - Documentação da API";
				options.DefaultModelsExpandDepth(2);
				options.DisplayRequestDuration();
			});

			app.MapScalarApiReference(options =>
			{
				options.Layout = ScalarLayout.Classic;
			});
		}
	}

	private sealed class InfoDocumentationTransformer : IOpenApiDocumentTransformer
	{
		public Task TransformAsync
		(
			OpenApiDocument document,
			OpenApiDocumentTransformerContext context,
			CancellationToken cancellationToken
		)
		{
			document.Info = new OpenApiInfo
			{
				Title = "FCG - Serviço de Notificação",
				Version = "v1",
				Description = """
					Microsserviço de notificação do FIAP Cloud Game
					""",
				Contact = new OpenApiContact
				{
					Name = "11NETTG30",
					Url = new Uri("https://github.com/11NETTG30/fcg-notifications")
				}
			};

			return Task.CompletedTask;
		}
	}

	private sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
	{
		private readonly IAuthenticationSchemeProvider _authenticationSchemeProvider;

		public BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider)
		{
			_authenticationSchemeProvider = authenticationSchemeProvider;
		}

		public async Task TransformAsync
		(
			OpenApiDocument document,
			OpenApiDocumentTransformerContext context,
			CancellationToken cancellationToken
		)
		{
			IEnumerable<AuthenticationScheme> authenticationSchemes = await _authenticationSchemeProvider.GetAllSchemesAsync();

			if (authenticationSchemes.Any(authScheme => authScheme.Name == "Bearer"))
			{
				Dictionary<string, IOpenApiSecurityScheme> securitySchemes = new()
				{
					["Bearer"] = new OpenApiSecurityScheme
					{
						Type = SecuritySchemeType.Http,
						Scheme = "bearer",
						In = ParameterLocation.Header,
						BearerFormat = "Json Web Token"
					}
				};
				document.Components ??= new OpenApiComponents();
				document.Components.SecuritySchemes = securitySchemes;

				IEnumerable<KeyValuePair<HttpMethod, OpenApiOperation>> operations = document.Paths.Values
					.SelectMany(path => path.Operations);

				foreach (KeyValuePair<HttpMethod, OpenApiOperation> operation in operations)
				{
					operation.Value.Security ??= [];
					operation.Value.Security.Add(new OpenApiSecurityRequirement
					{
						[new OpenApiSecuritySchemeReference("Bearer", document)] = []
					});
				}
			}
		}
	}
}