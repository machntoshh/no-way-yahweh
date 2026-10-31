// Builds extension/verses.json — the single verse source used by the extension (blocked page)
// and embedded in the Windows app (challenge screen).
//   python -I tools/verses/extract.py <chapters> <refs...> > verses.jsonl
//   node tools/verses/build.mjs verses.jsonl
// Verse texts are verbatim from the Bíblia Livre; reflections are ours. The only edit to the
// source text is dropping section headings that are not verse text (e.g. "Bete :" in Psalm 119).
import { readFileSync, writeFileSync } from "node:fs";

export const ATTRIBUTION =
  "Bíblia Livre (BLIVRE), © 2018 Diego Santos, Mario Sérgio e Marco Teles — CC BY 4.0, versão 2022-03-08, ebible.org/porbr2018. Títulos de seção removidos.";

// chapter id → [reference as shown, theme, reflection]
const META = {
  "1CO06": ["1 Coríntios 6:18", "pureza", "Fugir não é fraqueza; é sabedoria. Afastar-se agora já é uma vitória."],
  "1CO10": ["1 Coríntios 10:13", "tentação", "Esta pausa pode ser a saída que Deus preparou para hoje. Ninguém precisa enfrentar essa luta sem ajuda."],
  "MAT05": ["Mateus 5:28", "pureza", "Jesus olha para o coração, não só para os atos. Guardar o olhar é cuidar de quem você está se tornando."],
  "GAL05": ["Gálatas 5:22-23", "autocontrole", "Domínio próprio é fruto: cresce aos poucos, com tempo e cuidado. Cada escolha conta."],
  "2TI02": ["2 Timóteo 2:22", "disciplina", "Não é só fugir de algo: é correr em direção ao que é bom, junto de outras pessoas."],
  "PSA119": ["Salmos 119:9-11", "pureza", "A palavra guardada no coração é proteção que vai com você para qualquer lugar."],
  "PHP04": ["Filipenses 4:8", "renovação da mente", "Troque este momento por algo verdadeiro e bom: uma oração, uma conversa, uma caminhada."],
  "ROM12": ["Romanos 12:2", "renovação da mente", "A mente é renovada aos poucos. Hábitos novos estão sendo construídos agora."],
  "ROM08": ["Romanos 8:1", "graça", "Nenhuma condenação. Sua luta não define seu valor; a graça define."],
  "1JN01": ["1 João 1:9", "perdão", "Se você caiu, o caminho de volta está aberto. Perdão não é prêmio por desempenho."],
  "LAM03": ["Lamentações 3:22-23", "graça", "Hoje é uma manhã nova. As misericórdias também são."],
  "HEB04": ["Hebreus 4:15-16", "tentação", "Jesus entende a tentação por dentro. Você pode se aproximar com confiança, não com medo."],
  "JAS01": ["Tiago 1:12", "perseverança", "Resistir uma vez já é perseverar. Amanhã você resiste de novo, um dia de cada vez."],
  "PSA051": ["Salmos 51:10", "renovação", "Um coração puro é algo que Deus cria em nós — não precisamos produzir sozinhos."],
  "ISA40": ["Isaías 40:31", "perseverança", "Quando as forças acabam, ainda há de onde tirar mais. Confie e continue caminhando."],
  "PRO24": ["Provérbios 24:16", "recomeço", "Cair não é o fim da história. Levantar-se de novo é o que importa."],
};

const SECTION_HEADING = /^[A-ZÀ-Ú][a-zà-ú]+ : /; // e.g. "Bete : " (Hebrew letter headings in Psalm 119)

const verses = readFileSync(process.argv[2], "utf8").trim().split(/\r?\n/).map((line) => {
  const { chapter, text } = JSON.parse(line);
  const [reference, theme, reflection] = META[chapter];
  return { reference, theme, text: text.replace(SECTION_HEADING, ""), reflection };
});

writeFileSync(new URL("../../extension/verses.json", import.meta.url), JSON.stringify({ attribution: ATTRIBUTION, verses }, null, 2) + "\n");
console.log(`${verses.length} versículos`);
