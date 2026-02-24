# Google Cloud Text-to-Speech Setup Guide

## 1. Create Google Cloud Project

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing one
3. Enable **Cloud Text-to-Speech API**:
   - Search for "Text-to-Speech API"
   - Click "Enable"

## 2. Create Service Account

1. Go to **IAM & Admin > Service Accounts**
2. Click **Create Service Account**
3. Enter details:
   - Name: `vk-street-food-tts`
   - Description: `TTS service for VK Street Food app`
4. Grant role: **Cloud Text-to-Speech User**
5. Click **Done**

## 3. Generate API Key

1. Click on the created service account
2. Go to **Keys** tab
3. Click **Add Key > Create new key**
4. Select **JSON** format
5. Download the JSON key file

## 4. Configure Local Environment

### Windows (PowerShell)
```powershell
$env:GOOGLE_APPLICATION_CREDENTIALS="C:\path\to\your\service-account-key.json"
```

### Linux/Mac
```bash
export GOOGLE_APPLICATION_CREDENTIALS="/path/to/your/service-account-key.json"
```

### Permanent Setup (Windows)
1. Press `Win + X` → System
2. Advanced system settings → Environment Variables
3. Add new **System variable**:
   - Name: `GOOGLE_APPLICATION_CREDENTIALS`
   - Value: `C:\path\to\your\service-account-key.json`

## 5. Test TTS Service

Run API and test endpoint:
```bash
POST http://localhost:5089/api/audio/generate
Content-Type: application/json

{
  "poiId": 1,
  "languageCode": "vi",
  "voiceName": "vi-VN-Wavenet-A"
}
```

## 6. Available Voices

### Vietnamese (vi-VN)
- `vi-VN-Wavenet-A` - Female (default)
- `vi-VN-Wavenet-B` - Male
- `vi-VN-Wavenet-C` - Female
- `vi-VN-Wavenet-D` - Male

### English (en-US)
- `en-US-Wavenet-C` - Female (default)
- `en-US-Wavenet-D` - Male
- `en-US-Wavenet-E` - Female
- `en-US-Wavenet-F` - Female

### Korean (ko-KR)
- `ko-KR-Wavenet-A` - Female (default)
- `ko-KR-Wavenet-B` - Female
- `ko-KR-Wavenet-C` - Male
- `ko-KR-Wavenet-D` - Male

## 7. Pricing (Free Tier)

✅ **Free Tier**: 1 million characters per month (WaveNet voices)
- After free tier: $16 per 1 million characters
- Vietnamese Street Food descriptions: ~200-500 chars each
- 12 POIs × 3 languages = 36 audio files
- **Total**: ~12,000 characters (FREE)

## 8. Generate All Audio Files

Use this script to generate audio for all 12 POIs:

```bash
# Vietnamese
for i in {1..12}; do
  curl -X POST http://localhost:5089/api/audio/generate \
    -H "Content-Type: application/json" \
    -d "{\"poiId\": $i, \"languageCode\": \"vi\"}"
done

# English
for i in {1..12}; do
  curl -X POST http://localhost:5089/api/audio/generate \
    -H "Content-Type: application/json" \
    -d "{\"poiId\": $i, \"languageCode\": \"en\"}"
done

# Korean
for i in {1..12}; do
  curl -X POST http://localhost:5089/api/audio/generate \
    -H "Content-Type: application/json" \
    -d "{\"poiId\": $i, \"languageCode\": \"ko\"}"
done
```

## 9. Troubleshooting

### Error: "The Application Default Credentials are not available"
**Solution**: Set `GOOGLE_APPLICATION_CREDENTIALS` environment variable

### Error: "Permission denied"
**Solution**: Ensure service account has **Cloud Text-to-Speech User** role

### Error: "Quota exceeded"
**Solution**: Check quota in Google Cloud Console → IAM & Admin → Quotas

### Error: "Invalid voice name"
**Solution**: Use `GET /api/audio/poi/{poiId}/languages` to check available voices

## 10. Production Deployment

For production, use **Secret Manager** instead of environment variables:

1. Upload JSON key to Google Secret Manager
2. Grant service access to secret
3. Fetch credentials at runtime

## Notes

- Audio files saved to: `wwwroot/audio/`
- File naming: `{languageCode}_{guid}.mp3`
- URLs: `/audio/{languageCode}_{guid}.mp3`
- Duration: Auto-estimated (150 words/minute)
