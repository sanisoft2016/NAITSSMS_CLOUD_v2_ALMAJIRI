using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class TokenDto
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }

        [JsonPropertyName("exp")]
        public long? Exp { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
        
        [JsonExtensionData]
        public Dictionary<string, object>? AdditionalData { get; set; }
    }
}
