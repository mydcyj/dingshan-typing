import json
import re
import sys
from collections import Counter
from pathlib import Path


def main():
    source = Path(sys.argv[1])
    target = Path(__file__).with_name("Cet4.tsv")
    data = json.loads(source.read_text(encoding="utf-8-sig"))
    words = {}
    for entry in data["words"]:
        word = entry["word"].lower()
        if not re.fullmatch(r"[a-z]+", word) or not (
            len(word) in (3, 6, 7, 8, 10) or 11 <= len(word) <= 20
        ):
            continue
        for translation in entry["translations"]:
            translation = re.sub(r"^(?:[a-z]+[.]\s*|\[[^\]]+\]\s*)+", "", translation)
            depth = 0
            start = 0
            meaning = ""
            for index, character in enumerate(translation + "；"):
                if character in "(（":
                    depth += 1
                elif character in ")）":
                    depth = max(0, depth - 1)
                elif character in ",，;；、。" and depth == 0:
                    part = translation[start:index]
                    if re.search(r"[\u3400-\u9fff]", part):
                        meaning = part
                        break
                    start = index + 1
            if meaning:
                words.setdefault(word, " ".join(meaning.split()))
                break

    counts = Counter(map(len, words))
    if any(counts[length] < 8 for length in (3, 6, 7, 8, 10)) or sum(
        count for length, count in counts.items() if length >= 11
    ) < 2:
        raise ValueError("The word list does not cover all roles.")
    target.write_text("".join(f"{word}\t{meaning}\n" for word, meaning in sorted(words.items())), encoding="utf-8")
    print(f"{len(words)} words, by length: {sorted(counts.items())}")


if __name__ == "__main__":
    main()
