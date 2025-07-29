// See https://aka.ms/new-console-template for more information

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MemoryPack;

string inputFilePath = "localsave.bytes";
string outputFilePath = "localsave_modified.bytes"; 

if (!File.Exists(inputFilePath))
{
    Console.WriteLine($"오류: '{inputFilePath}' 파일을 찾을 수 없습니다.");
    return;
}

try
{
    Console.WriteLine($"'{inputFilePath}' 파일 읽기 및 압축 해제 중...");
    byte[] compressedBytes = File.ReadAllBytes(inputFilePath);
    byte[] decompressedBytes = DecompressBrotli(compressedBytes);
    Console.WriteLine($"압축 해제 완료. 원본 크기: {decompressedBytes.Length} 바이트");
    
    string regexPattern = Regex.Escape("LanguageIndex_") + "(.*?)" + Regex.Escape("/Star_Data/StreamingAssets");
    Regex regex = new Regex(regexPattern, RegexOptions.Singleline | RegexOptions.Compiled);
    
    string decompressedText = Encoding.UTF8.GetString(decompressedBytes);

    MatchCollection matches = regex.Matches(decompressedText);

    if (matches.Count == 0)
    {
        Console.WriteLine($"오류: '{regexPattern}' 패턴과 일치하는 'LanguageIndex' 키를 찾을 수 없습니다.");
        return;
    }

    int matchesFoundCount = 0;
    int currentByteSearchStartIndex = 0;

    Console.WriteLine($"\n총 {matches.Count}개의 'LanguageIndex' 키를 찾았습니다. 값 변경 시도...");

    foreach (Match match in matches)
    {
        byte[] searchKeyBytes = Encoding.UTF8.GetBytes(match.Value);
        
        int actualKeyByteStartIndex = FindBytes(decompressedBytes, searchKeyBytes, currentByteSearchStartIndex);

        if (actualKeyByteStartIndex == -1)
        {
            Console.WriteLine($"경고: Regex로 찾은 키 '{match.Value}' (텍스트 인덱스: {match.Index})를 바이트 배열에서 찾을 수 없습니다 (현재 바이트 검색 시작: {currentByteSearchStartIndex}). 이 항목은 건너뜝니다.");
            continue;
        }
        
        currentByteSearchStartIndex = actualKeyByteStartIndex + searchKeyBytes.Length;
        
        int valueStartOffsetFromKeyEnd = 9; 
        int valueStartIndex = actualKeyByteStartIndex + searchKeyBytes.Length + valueStartOffsetFromKeyEnd;
        
        if (valueStartIndex + 1 > decompressedBytes.Length)
        {
            Console.WriteLine($"오류: 키 '{match.Value}'에 대한 값 데이터를 쓸 공간이 부족합니다 (예상치 못한 데이터 구조). 이 항목은 건너뜝니다.");
            continue;
        }
        
        var originalValue = decompressedBytes[valueStartIndex];
        Console.WriteLine($@"
----------------------------------------------------
키 '{match.Value}' (바이트 인덱스: {actualKeyByteStartIndex})
이전 LanguageIndex 값: {originalValue}
----------------------------------------------------");
        
        byte newValue = 1;
        decompressedBytes[valueStartIndex] = newValue; ;
        Console.WriteLine($"LanguageIndex 값을 {newValue}로 변경 완료.");
        matchesFoundCount++;
    }

    Console.WriteLine($"\n총 {matchesFoundCount}개의 'LanguageIndex' 값을 변경했습니다.");
    
    Console.WriteLine("\n수정된 데이터를 Brotli로 다시 압축 중...");
    byte[] reCompressedBytes = CompressBrotli(decompressedBytes);
    Console.WriteLine($"재압축 완료. 새 압축된 데이터 크기: {reCompressedBytes.Length} 바이트");
    
    File.WriteAllBytes(outputFilePath, reCompressedBytes);
    Console.WriteLine($"\n수정된 파일이 '{outputFilePath}' 경로에 성공적으로 저장되었습니다.");
    
    Console.WriteLine($"{inputFilePath} 파일을 백업하였습니다.{inputFilePath}.bak");
    File.Copy(inputFilePath, inputFilePath+".bak", true);
    
    File.Copy(outputFilePath, inputFilePath, true);
    File.Delete(outputFilePath);
    
}
catch (Exception ex)
{
    Console.WriteLine($"예상치 못한 오류 발생: {ex.Message}");
    Console.WriteLine("스택 추적:\n" + ex.StackTrace);
}
Console.WriteLine("패치가 완료되었습니다. 5초후 자동으로 콘솔창이 닫힙니다.");
Thread.Sleep(5000);
return;

static byte[] DecompressBrotli(byte[] compressedData)
{
    using var inputStream = new MemoryStream(compressedData);
    using var brotliStream = new BrotliStream(inputStream, CompressionMode.Decompress);
    using var outputStream = new MemoryStream();
    brotliStream.CopyTo(outputStream);
    return outputStream.ToArray();
}

static byte[] CompressBrotli(byte[] decompressedData, int qualityLevel = 11)
{
    using var outputStream = new MemoryStream();
    using var brotliStream = new BrotliStream(outputStream, CompressionMode.Compress, true); 
    brotliStream.Write(decompressedData, 0, decompressedData.Length);
    brotliStream.Dispose(); 
    return outputStream.ToArray();
}

static int FindBytes(byte[] source, byte[] pattern, int startIndex = 0)
{
    if (pattern.Length == 0 || startIndex < 0 || startIndex >= source.Length)
    {
        return -1;
    }
    if (pattern.Length > source.Length - startIndex)
    {
        return -1;
    }

    for (int i = startIndex; i <= source.Length - pattern.Length; i++)
    {
        bool found = true;
        for (int j = 0; j < pattern.Length; j++)
        {
            if (source[i + j] != pattern[j])
            {
                found = false;
                break;
            }
        }
        if (found)
        {
            return i;
        }
    }
    return -1;
}