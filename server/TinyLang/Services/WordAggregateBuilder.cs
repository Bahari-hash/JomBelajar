using System.Runtime.CompilerServices;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Exceptions;

[assembly: InternalsVisibleTo("TinyLang.UnitTests")]

namespace TinyLang.Services;

/// <summary>
/// 构建并规范化不依赖数据库的词条聚合。
/// </summary>
internal static class WordAggregateBuilder
{
    public static Word Create(CreateWordRequest request)
    {
        var identity = NormalizeIdentity(request.Headword);
        var word = new Word
        {
            Headword = identity.Headword,
            NormalizedHeadword = identity.NormalizedHeadword,
            AudioResourceId = request.AudioResourceId
        };
        foreach (var senseInput in request.Senses)
        {
            word.Senses.Add(CreateSense(word, senseInput));
        }
        return word;
    }

    public static WordIdentity NormalizeIdentity(string headword)
    {
        if (string.IsNullOrWhiteSpace(headword))
        {
            throw new RequestValidationException(ErrorCodes.WordHeadwordRequired);
        }

        var display = WordTextNormalizer.NormalizeHeadwordForDisplay(headword);
        var comparisonKey = WordTextNormalizer.CreateHeadwordComparisonKey(headword);
        if (display.Length > WordConstraints.MaxHeadwordLength ||
            comparisonKey.Length > WordConstraints.MaxHeadwordLength)
        {
            throw new RequestValidationException(ErrorCodes.WordHeadwordLengthLimit);
        }
        return new WordIdentity(display, comparisonKey);
    }

    public static WordSense CreateSense(Word word, WordSenseInput input)
    {
        var sense = new WordSense
        {
            WordId = word.Id,
            Word = word,
            Definition = string.Empty
        };
        ApplySenseValues(sense, input);
        foreach (var exampleInput in input.Examples)
        {
            sense.Examples.Add(CreateExample(sense, exampleInput));
        }
        return sense;
    }

    public static void ApplySenseValues(WordSense sense, WordSenseInput input)
    {
        sense.PartOfSpeech = input.PartOfSpeech;
        sense.Definition = input.Definition.Trim();
        sense.UsageNote = string.IsNullOrWhiteSpace(input.UsageNote)
            ? null
            : input.UsageNote.Trim();
        sense.SortOrder = input.SortOrder;
    }

    public static ExampleSentence CreateExample(
        WordSense sense,
        ExampleSentenceInput input)
    {
        var example = new ExampleSentence
        {
            WordSenseId = sense.Id,
            WordSense = sense,
            Sentence = string.Empty,
            Translation = string.Empty
        };
        ApplyExampleValues(example, input);
        return example;
    }

    public static void ApplyExampleValues(
        ExampleSentence example,
        ExampleSentenceInput input)
    {
        example.AudioResourceId = input.AudioResourceId;
        example.Sentence = input.Sentence.Trim();
        example.Translation = input.Translation.Trim();
        example.SortOrder = input.SortOrder;
    }
}

internal readonly record struct WordIdentity(
    string Headword,
    string NormalizedHeadword);
