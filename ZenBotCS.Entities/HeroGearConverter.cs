using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

namespace ZenBotCS.Entities;

public class HeroGearConverter : JsonConverter<List<HeroGear>>
{
    public override void WriteJson(JsonWriter writer, List<HeroGear>? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        JArray array = [];
        foreach (var gear in value)
        {
            if (gear.Level == 0)
            {
                array.Add(gear.Name);
            }
            else
            {
                JObject obj = new JObject
            {
                { "name", gear.Name },
                { "level", gear.Level }
            };
                array.Add(obj);
            }
        }
        array.WriteTo(writer);
    }

    public override List<HeroGear> ReadJson(JsonReader reader, Type objectType, List<HeroGear>? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return [];
        }

        var heroGears = new List<HeroGear>();
        JArray array = JArray.Load(reader);

        foreach (var item in array)
        {
            if (item.Type == JTokenType.String)
            {
                heroGears.Add(new HeroGear { Name = item.ToString(), Level = 0 });
            }
            else if (item.Type == JTokenType.Object)
            {
                var heroGear = item.ToObject<HeroGear>();
                if (heroGear != null)
                {
                    heroGears.Add(heroGear);
                }
            }
        }

        return heroGears;
    }

    public override bool CanRead => true;
    public override bool CanWrite => true;
}
