using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

namespace ZenBotCS.Entities;

public class LegendsDataConverter : JsonConverter<LegendsData>
{
    public override LegendsData ReadJson(JsonReader reader, Type objectType, LegendsData? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var legendsData = new LegendsData();
        var jsonObject = JObject.Load(reader);

        foreach (var property in jsonObject.Properties())
        {
            if (property.Name == "global_rank")
            {
                legendsData.GlobalRank = property.Value.ToObject<int?>();
            }
            else if (property.Name == "local_rank")
            {
                legendsData.LocalRank = property.Value.ToObject<int?>();
            }
            else
            {
                var legendsDay = property.Value.ToObject<LegendsDay>();
                if (legendsDay != null)
                {
                    legendsData.LegendsDays[property.Name] = legendsDay;
                }
            }
        }

        return legendsData;
    }

    public override void WriteJson(JsonWriter writer, LegendsData? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();

        if (value.GlobalRank.HasValue)
        {
            writer.WritePropertyName("global_rank");
            writer.WriteValue(value.GlobalRank);
        }

        if (value.LocalRank.HasValue)
        {
            writer.WritePropertyName("local_rank");
            writer.WriteValue(value.LocalRank);
        }

        foreach (var entry in value.LegendsDays)
        {
            writer.WritePropertyName(entry.Key);
            serializer.Serialize(writer, entry.Value);
        }

        writer.WriteEndObject();
    }
}
