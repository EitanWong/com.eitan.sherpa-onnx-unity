//	Copyright (c) 2012 Calvin Rien
//        http://the.darktable.com
//
//	This software is provided 'as-is', without any express or implied warranty. In
//	no event will the authors be held liable for any damages arising from the use
//	of this software.
//
//	Permission is granted to anyone to use this software for any purpose,
//	including commercial applications, and to alter it and redistribute it freely,
//	subject to the following restrictions:
//
//	1. The origin of this software must not be misrepresented; you must not claim
//	that you wrote the original software. If you use this software in a product,
//	an acknowledgment in the product documentation would be appreciated but is not
//	required.
//
//	2. Altered source versions must be plainly marked as such, and must not be
//	misrepresented as being the original software.
//
//	3. This notice may not be removed or altered from any source distribution.
//
//  =============================================================================
//
//  derived from Gregorio Zanon's script
//  http://forum.unity3d.com/threads/119295-Writing-AudioListener.GetOutputData-to-wav-problem?p=806734&viewfull=1#post806734
//
//  FIXED: Multi-channel audio support

using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

public static class AudioExtension
{

	private const int HEADER_SIZE = 44;

	public static bool Save(this AudioClip clip, string filename = null)
	{
		//if filename is null, use current time as filename
		if (string.IsNullOrEmpty(filename))
		{
			filename = string.IsNullOrEmpty(clip.name) ? $"AudioClip_{clip.frequency}Hz_{clip.channels}ch_{clip.length}s_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}" : clip.name;
		}

		if (!filename.ToLower().EndsWith(".wav"))
		{
			filename += ".wav";
		}

		string directoryPath = Application.isMobilePlatform
			? Path.Combine(Application.persistentDataPath, "Recordings/Audio")
			: Path.Combine(System.IO.Directory.GetParent(Application.dataPath).ToString(), "Recordings/Audio");

		if (!Directory.Exists(directoryPath))
		{
			Directory.CreateDirectory(directoryPath);
		}
		var filepath = Path.Combine(directoryPath, filename);

		// Debug.Log($"[WavUtils] Saving {clip.channels}-channel WAV file to: {filepath}");
		// Debug.Log($"[WavUtils] Audio info - Samples: {clip.samples}, Channels: {clip.channels}, Frequency: {clip.frequency}Hz");

		// Make sure directory exists if user is saving to sub dir.
		Directory.CreateDirectory(Path.GetDirectoryName(filepath));

		using (var fileStream = CreateEmpty(filepath))
		{

			ConvertAndWrite(fileStream, clip);

			WriteHeader(fileStream, clip);
		}

		return true; // TODO: return false if there's a failure saving the file
	}

	public static AudioClip TrimSilence(AudioClip clip, float min)
	{
		var samples = new float[clip.samples * clip.channels]; // Fixed: account for all channels

		clip.GetData(samples, 0);

		return TrimSilence(new List<float>(samples), min, clip.channels, clip.frequency);
	}


	public static AudioClip TrimSilence(List<float> samples, float min, int channels, int hz)
	{
		int i;

		// Find start of audio (skip leading silence)
		for (i = 0; i < samples.Count; i++)
		{
			if (Mathf.Abs(samples[i]) > min)
			{
				break;
			}
		}

		samples.RemoveRange(0, i);

		// Find end of audio (skip trailing silence)
		for (i = samples.Count - 1; i > 0; i--)
		{
			if (Mathf.Abs(samples[i]) > min)
			{
				break;
			}
		}

		samples.RemoveRange(i, samples.Count - i);

		// Ensure sample count is divisible by channel count
		int validSampleCount = (samples.Count / channels) * channels;
		if (validSampleCount != samples.Count)
		{
			samples.RemoveRange(validSampleCount, samples.Count - validSampleCount);
		}

		var clip = AudioClip.Create("TempClip", samples.Count / channels, channels, hz, false);

		clip.SetData(samples.ToArray(), 0);

		return clip;
	}

	private static FileStream CreateEmpty(string filepath)
	{
		var fileStream = new FileStream(filepath, FileMode.Create);
		byte emptyByte = new byte();

		for (int i = 0; i < HEADER_SIZE; i++) //preparing the header
		{
			fileStream.WriteByte(emptyByte);
		}

		return fileStream;
	}

	private static void ConvertAndWrite(FileStream fileStream, AudioClip clip)
	{
		// FIXED: Properly handle multi-channel audio data

		// Total samples = samples per channel * number of channels
		int totalSamples = clip.samples * clip.channels;
		var samples = new float[totalSamples];

		// Get all interleaved sample data
		clip.GetData(samples, 0);

		// Debug.Log($"[WavUtils] Converting {totalSamples} total samples ({clip.samples} per channel × {clip.channels} channels)");

		// Convert float samples to 16-bit integers
		Int16[] intData = new Int16[totalSamples];
		Byte[] bytesData = new Byte[totalSamples * 2]; // 2 bytes per 16-bit sample

		int rescaleFactor = 32767; // Convert float to Int16

		for (int i = 0; i < totalSamples; i++)
		{
			// Clamp the sample to prevent overflow
			float clampedSample = Mathf.Clamp(samples[i], -1f, 1f);
			intData[i] = (short)(clampedSample * rescaleFactor);

			// Convert to bytes (little-endian)
			Byte[] byteArr = BitConverter.GetBytes(intData[i]);
			byteArr.CopyTo(bytesData, i * 2);
		}

		fileStream.Write(bytesData, 0, bytesData.Length);

		// Debug.Log($"[WavUtils] Wrote {bytesData.Length} bytes of audio data");
	}

	private static void WriteHeader(FileStream fileStream, AudioClip clip)
	{
		var hz = clip.frequency;
		var channels = clip.channels;
		var samplesPerChannel = clip.samples; // This is samples per channel, not total samples
		var totalSamples = samplesPerChannel * channels;

		// Debug.Log($"[WavUtils] Writing WAV header - {channels} channels, {hz}Hz, {samplesPerChannel} samples per channel");

		fileStream.Seek(0, SeekOrigin.Begin);

		// RIFF header
		Byte[] riff = System.Text.Encoding.UTF8.GetBytes("RIFF");
		fileStream.Write(riff, 0, 4);

		// File size - 8 bytes (for RIFF and file size fields)
		Byte[] chunkSize = BitConverter.GetBytes((int)(fileStream.Length - 8));
		fileStream.Write(chunkSize, 0, 4);

		// WAVE
		Byte[] wave = System.Text.Encoding.UTF8.GetBytes("WAVE");
		fileStream.Write(wave, 0, 4);

		// fmt chunk
		Byte[] fmt = System.Text.Encoding.UTF8.GetBytes("fmt ");
		fileStream.Write(fmt, 0, 4);

		// fmt chunk size (16 for PCM)
		Byte[] subChunk1 = BitConverter.GetBytes(16);
		fileStream.Write(subChunk1, 0, 4);

		// Audio format (1 for PCM)
		UInt16 one = 1;
		Byte[] audioFormat = BitConverter.GetBytes(one);
		fileStream.Write(audioFormat, 0, 2);

		// Number of channels
		Byte[] numChannels = BitConverter.GetBytes((UInt16)channels);
		fileStream.Write(numChannels, 0, 2);

		// Sample rate
		Byte[] sampleRate = BitConverter.GetBytes(hz);
		fileStream.Write(sampleRate, 0, 4);

		// Byte rate = sampleRate * channels * bytesPerSample
		Byte[] byteRate = BitConverter.GetBytes(hz * channels * 2);
		fileStream.Write(byteRate, 0, 4);

		// Block align = channels * bytesPerSample
		UInt16 blockAlign = (ushort)(channels * 2);
		fileStream.Write(BitConverter.GetBytes(blockAlign), 0, 2);

		// Bits per sample
		UInt16 bps = 16;
		Byte[] bitsPerSample = BitConverter.GetBytes(bps);
		fileStream.Write(bitsPerSample, 0, 2);

		// data chunk header
		Byte[] datastring = System.Text.Encoding.UTF8.GetBytes("data");
		fileStream.Write(datastring, 0, 4);

		// Data chunk size = totalSamples * bytesPerSample
		Byte[] subChunk2 = BitConverter.GetBytes(totalSamples * 2);
		fileStream.Write(subChunk2, 0, 4);

		// Debug.Log($"[WavUtils] WAV header written successfully");
	}
}
