namespace mvdmio.TranslationTools.Client.Placeholders;

internal static partial class PluralRules
{
   private static PluralCategory? Evaluate(string language, long n) => language switch
   {
      "af"
         or "an"
         or "asa"
         or "ast"
         or "az"
         or "bal"
         or "bem"
         or "bez"
         or "bg"
         or "brx"
         or "ce"
         or "cgg"
         or "chr"
         or "ckb"
         or "da"
         or "de"
         or "dv"
         or "ee"
         or "el"
         or "en"
         or "eo"
         or "et"
         or "eu"
         or "fi"
         or "fo"
         or "fur"
         or "fy"
         or "gsw"
         or "ha"
         or "haw"
         or "hu"
         or "ia"
         or "ie"
         or "io"
         or "jgo"
         or "jmc"
         or "ka"
         or "kaj"
         or "kcg"
         or "kk"
         or "kkj"
         or "kl"
         or "ks"
         or "ksb"
         or "ku"
         or "ky"
         or "lb"
         or "lg"
         or "lij"
         or "mas"
         or "mgo"
         or "ml"
         or "mn"
         or "mr"
         or "nah"
         or "nb"
         or "nd"
         or "ne"
         or "nl"
         or "nn"
         or "nnh"
         or "no"
         or "nr"
         or "ny"
         or "nyn"
         or "om"
         or "or"
         or "os"
         or "pap"
         or "ps"
         or "rm"
         or "rof"
         or "rwk"
         or "saq"
         or "sc"
         or "sd"
         or "sdh"
         or "seh"
         or "sn"
         or "so"
         or "sq"
         or "ss"
         or "ssy"
         or "st"
         or "sv"
         or "sw"
         or "syr"
         or "ta"
         or "te"
         or "teo"
         or "tig"
         or "tk"
         or "tn"
         or "tr"
         or "ts"
         or "ug"
         or "ur"
         or "uz"
         or "ve"
         or "vo"
         or "vun"
         or "wae"
         or "xh"
         or "xog"
         or "yi" =>
         n is 1 ? PluralCategory.One
            : PluralCategory.Other,
      "ak"
         or "bho"
         or "csw"
         or "guw"
         or "ln"
         or "mg"
         or "nso"
         or "pa"
         or "ti"
         or "wa" =>
         n is >= 0 and <= 1 ? PluralCategory.One
            : PluralCategory.Other,
      "am"
         or "as"
         or "bn"
         or "doi"
         or "fa"
         or "gu"
         or "hi"
         or "kn"
         or "kok"
         or "kok-latn"
         or "pcm"
         or "tg"
         or "vi"
         or "zu" =>
         n is 0 || n is 1 ? PluralCategory.One
            : PluralCategory.Other,
      "ar"
         or "ars" =>
         n is 0 ? PluralCategory.Zero
            : n is 1 ? PluralCategory.One
            : n is 2 ? PluralCategory.Two
            : n % 100 is >= 3 and <= 10 ? PluralCategory.Few
            : n % 100 is >= 11 and <= 99 ? PluralCategory.Many
            : PluralCategory.Other,
      "be"
         or "ru"
         or "uk" =>
         n % 10 is 1 && n % 100 is not 11 ? PluralCategory.One
            : n % 10 is >= 2 and <= 4 && n % 100 is not (>= 12 and <= 14) ? PluralCategory.Few
            : n % 10 is 0 || n % 10 is >= 5 and <= 9 || n % 100 is >= 11 and <= 14 ? PluralCategory.Many
            : PluralCategory.Other,
      "blo"
         or "cv"
         or "ksh" =>
         n is 0 ? PluralCategory.Zero
            : n is 1 ? PluralCategory.One
            : PluralCategory.Other,
      "bm"
         or "bo"
         or "dz"
         or "hnj"
         or "id"
         or "ig"
         or "ii"
         or "ja"
         or "jbo"
         or "jv"
         or "jw"
         or "kde"
         or "kea"
         or "km"
         or "ko"
         or "lkt"
         or "lo"
         or "ms"
         or "my"
         or "nqo"
         or "osa"
         or "sah"
         or "ses"
         or "sg"
         or "su"
         or "th"
         or "to"
         or "tpi"
         or "und"
         or "wo"
         or "yo"
         or "yue"
         or "zh" =>
         PluralCategory.Other,
      "br" =>
         n % 10 is 1 && n % 100 is not (11 or 71 or 91) ? PluralCategory.One
            : n % 10 is 2 && n % 100 is not (12 or 72 or 92) ? PluralCategory.Two
            : n % 10 is (>= 3 and <= 4) or 9 && n % 100 is not ((>= 10 and <= 19) or (>= 70 and <= 79) or (>= 90 and <= 99)) ? PluralCategory.Few
            : n is not 0 && n % 1000000 is 0 ? PluralCategory.Many
            : PluralCategory.Other,
      "bs"
         or "hr"
         or "sh"
         or "sr" =>
         n % 10 is 1 && n % 100 is not 11 ? PluralCategory.One
            : n % 10 is >= 2 and <= 4 && n % 100 is not (>= 12 and <= 14) ? PluralCategory.Few
            : PluralCategory.Other,
      "ca"
         or "es"
         or "gl"
         or "it"
         or "lld"
         or "pt-pt"
         or "scn"
         or "vec" =>
         n is 1 ? PluralCategory.One
            : n is not 0 && n % 1000000 is 0 ? PluralCategory.Many
            : PluralCategory.Other,
      "ceb"
         or "fil"
         or "tl" =>
         n is 1 or 2 or 3 || n % 10 is not (4 or 6 or 9) ? PluralCategory.One
            : PluralCategory.Other,
      "cs"
         or "sk" =>
         n is 1 ? PluralCategory.One
            : n is >= 2 and <= 4 ? PluralCategory.Few
            : PluralCategory.Other,
      "cy" =>
         n is 0 ? PluralCategory.Zero
            : n is 1 ? PluralCategory.One
            : n is 2 ? PluralCategory.Two
            : n is 3 ? PluralCategory.Few
            : n is 6 ? PluralCategory.Many
            : PluralCategory.Other,
      "dsb"
         or "hsb"
         or "sl" =>
         n % 100 is 1 ? PluralCategory.One
            : n % 100 is 2 ? PluralCategory.Two
            : n % 100 is >= 3 and <= 4 ? PluralCategory.Few
            : PluralCategory.Other,
      "ff"
         or "hy"
         or "kab"
         or "si" =>
         n is 0 or 1 ? PluralCategory.One
            : PluralCategory.Other,
      "fr"
         or "pt" =>
         n is >= 0 and <= 1 ? PluralCategory.One
            : n is not 0 && n % 1000000 is 0 ? PluralCategory.Many
            : PluralCategory.Other,
      "ga" =>
         n is 1 ? PluralCategory.One
            : n is 2 ? PluralCategory.Two
            : n is >= 3 and <= 6 ? PluralCategory.Few
            : n is >= 7 and <= 10 ? PluralCategory.Many
            : PluralCategory.Other,
      "gd" =>
         n is 1 or 11 ? PluralCategory.One
            : n is 2 or 12 ? PluralCategory.Two
            : n is (>= 3 and <= 10) or (>= 13 and <= 19) ? PluralCategory.Few
            : PluralCategory.Other,
      "gv" =>
         n % 10 is 1 ? PluralCategory.One
            : n % 10 is 2 ? PluralCategory.Two
            : n % 100 is 0 or 20 or 40 or 60 or 80 ? PluralCategory.Few
            : PluralCategory.Other,
      "he"
         or "iu"
         or "naq"
         or "sat"
         or "se"
         or "sma"
         or "smi"
         or "smj"
         or "smn"
         or "sms" =>
         n is 1 ? PluralCategory.One
            : n is 2 ? PluralCategory.Two
            : PluralCategory.Other,
      "is"
         or "mk" =>
         n % 10 is 1 && n % 100 is not 11 ? PluralCategory.One
            : PluralCategory.Other,
      "kw" =>
         n is 0 ? PluralCategory.Zero
            : n is 1 ? PluralCategory.One
            : n % 100 is 2 or 22 or 42 or 62 or 82 || (n % 1000 is 0 && n % 100000 is (>= 1000 and <= 20000) or 40000 or 60000 or 80000) || (n is not 0 && n % 1000000 is 100000) ? PluralCategory.Two
            : n % 100 is 3 or 23 or 43 or 63 or 83 ? PluralCategory.Few
            : n is not 1 && n % 100 is 1 or 21 or 41 or 61 or 81 ? PluralCategory.Many
            : PluralCategory.Other,
      "lag" =>
         n is 0 ? PluralCategory.Zero
            : n is 0 or 1 && n is not 0 ? PluralCategory.One
            : PluralCategory.Other,
      "lt" =>
         n % 10 is 1 && n % 100 is not (>= 11 and <= 19) ? PluralCategory.One
            : n % 10 is >= 2 and <= 9 && n % 100 is not (>= 11 and <= 19) ? PluralCategory.Few
            : PluralCategory.Other,
      "lv"
         or "prg" =>
         n % 10 is 0 || n % 100 is >= 11 and <= 19 ? PluralCategory.Zero
            : n % 10 is 1 && n % 100 is not 11 ? PluralCategory.One
            : PluralCategory.Other,
      "mo"
         or "ro" =>
         n is 1 ? PluralCategory.One
            : n is 0 || (n is not 1 && n % 100 is >= 1 and <= 19) ? PluralCategory.Few
            : PluralCategory.Other,
      "mt" =>
         n is 1 ? PluralCategory.One
            : n is 2 ? PluralCategory.Two
            : n is 0 || n % 100 is >= 3 and <= 10 ? PluralCategory.Few
            : n % 100 is >= 11 and <= 19 ? PluralCategory.Many
            : PluralCategory.Other,
      "pl" =>
         n is 1 ? PluralCategory.One
            : n % 10 is >= 2 and <= 4 && n % 100 is not (>= 12 and <= 14) ? PluralCategory.Few
            : (n is not 1 && n % 10 is >= 0 and <= 1) || n % 10 is >= 5 and <= 9 || n % 100 is >= 12 and <= 14 ? PluralCategory.Many
            : PluralCategory.Other,
      "sgs" =>
         n % 10 is 1 && n % 100 is not 11 ? PluralCategory.One
            : n is 2 ? PluralCategory.Two
            : n is not 2 && n % 10 is >= 2 and <= 9 && n % 100 is not (>= 11 and <= 19) ? PluralCategory.Few
            : PluralCategory.Other,
      "shi" =>
         n is 0 || n is 1 ? PluralCategory.One
            : n is >= 2 and <= 10 ? PluralCategory.Few
            : PluralCategory.Other,
      "tzm" =>
         n is >= 0 and <= 1 || n is >= 11 and <= 99 ? PluralCategory.One
            : PluralCategory.Other,
      _ => null,
   };
}
