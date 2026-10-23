from pathlib import Path
from docx import Document
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

source_report = Path(r"C:\Users\eg0rg\Desktop\quizzy\docs\Отчет_ПЗ2_Модульная_архитектура.docx")
report = Path(r"C:\Users\eg0rg\Desktop\quizzy\docs\Отчет_ПЗ2_Модульная_архитектура_с_Р7.docx")
evidence = Path(r"C:\Users\eg0rg\Desktop\quizzy\_pz02_work\evidence-p7-answer-scoring-crop.png")
doc = Document(source_report)

def replace_paragraph(paragraph, text):
    for child in list(paragraph._p):
        if child.tag != qn("w:pPr"):
            paragraph._p.remove(child)
    paragraph.add_run(text)

def insert_after(paragraph, text):
    from docx.text.paragraph import Paragraph
    element = OxmlElement("w:p")
    paragraph._p.addnext(element)
    new_paragraph = Paragraph(element, paragraph._parent)
    new_paragraph.style = paragraph.style
    new_paragraph.add_run(text)
    return new_paragraph

paras = doc.paragraphs
# Keep the hand-maintained table of contents aligned with the rendered page numbers.
toc_pages = {
    "4.2 Взаимная проверка (Р7)": "4.2 Взаимная проверка (Р7)\t15",
    "ЗАКЛЮЧЕНИЕ": "ЗАКЛЮЧЕНИЕ\t17",
    "СПИСОК ИСПОЛЬЗОВАННЫХ МАТЕРИАЛОВ": "СПИСОК ИСПОЛЬЗОВАННЫХ МАТЕРИАЛОВ\t18",
}
for paragraph in paras:
    for prefix, value in toc_pages.items():
        if paragraph.text.startswith(prefix) and "\t" in paragraph.text:
            replace_paragraph(paragraph, value)

# The cover page lists group S324; repeat the same value in the work metadata.
student_row = next(row for table in doc.tables for row in table.rows if len(row.cells) > 1 and row.cells[0].text == "Студент, группа")
student_row.cells[1].text = "Егор Голубев; группа С324"

introduction = next(p for p in paras if p.text.startswith("В этой работе выделен модуль регистрации"))
replace_paragraph(
    introduction,
    "В этой работе выделен модуль регистрации и входа Quizzy. Контракт отделяет правила учётной записи и работу с хранилищем от HTTP-контроллера. Рабочая реализация и тестовая подстановка подключаются за одним интерфейсом. Взаимная проверка модуля начисления баллов другого студента проведена; результаты и обнаруженные ограничения приведены в разделе 4.2.",
)

target = next(p for p in paras if p.text.startswith("Р7 не выполнен:"))
replace_paragraph(
    target,
    "22 октября 2026 г. выполнена независимая проверка AnswerScoringService (QuizMvp/Services/AnswerScoringService.cs); автор последнего изменения по Git — yunna. Проверялась чистая версия проекта cca508ce642c55369d541daf01d4d78e9f91acf1; файл модуля добавлен в commit 7a35337eac03bead139be17004e38201c813fec9. В среде Windows / .NET SDK 10.0.401 восстановление зависимостей прошло; Release-сборка завершилась с 0 ошибками и 34 предупреждениями; 19 тестов QuizMvp.Tests прошли.",
)
second = insert_after(
    target,
    "Девять сценариев QuizMvp.LogicTests прошли: точное и переставленное совпадение, неполный/лишний/пустой выбор, дубликаты, пустой набор правильных ответов, null и отрицательные баллы. Режим --mutation-check обнаружил модель дефекта игнорирования лишнего ответа (TC-SCORE-04). В реализации теста это локальная функция MutatedScore, а не изменение исходника модуля; результат подтверждает чувствительность случая, но не является полноценным mutation testing. QuizController.SubmitAnswer передаёт результат в запись ответа и HTTP-ответ; отдельного интеграционного теста этого сценария начисления и сохранения в наборе xUnit нет.",
)
insert_after(
    second,
    "Зависимость QuizController → AnswerScoringService подтверждается прямым вызовом; в текущей рабочей карте она показана, но автор и версия записи не указаны, а карта не входит в проверяемый commit. Замечания: модуль статический и вызывается напрямую, второй реализации и демонстрации подмены нет; отдельная документация контракта Score и XML-комментарии не найдены. Итог Р7: проверка выполнена, функциональные сценарии пройдены; отмеченные архитектурные и документальные ограничения, включая отсутствие подмены, зафиксированы в протоколе.",
)

# Replace the existing status-card image with the captured test-console screenshot.
image_paragraph = next(p for p in doc.paragraphs if p._p.xpath(".//a:blip") and p._p.xpath(".//a:blip")[0].get(qn("r:embed")) == "rId15")
rel = doc.part.rels["rId15"]
rel.target_part._blob = evidence.read_bytes()

caption = next(p for p in doc.paragraphs if p.text.startswith("Рисунок 6 —"))
replace_paragraph(caption, "Рисунок 6 — Результаты проверки AnswerScoringService")

conclusion = next(p for p in doc.paragraphs if p.text.startswith("В проекте выделен и интегрирован модуль регистрации"))
replace_paragraph(
    conclusion,
    "В проекте выделен и интегрирован модуль регистрации и входа с документированным контрактом. AccountController зависит от контракта и принимает рабочую или тестовую реализацию без изменения сценария. Все 25 локальных тестов авторизационного модуля прошли. Взаимная проверка AnswerScoringService выполнена: девять проверок логики прошли; локальная модель мутанта с лишним выбранным вариантом обнаружена случаем TC-SCORE-04. Полноценное изменение исходника при мутационной проверке не выполнялось. Для проверенного модуля зафиксированы отсутствие подмены реализации и отдельной документации контракта; результаты отражены в протоколе Р7.",
)

source = next(p for p in doc.paragraphs if p.text.startswith("4) Исходный код QuizMvp"))
replace_paragraph(
    source,
    "4) Исходный код QuizMvp, tests/QuizMvp.Tests и QuizMvp.LogicTests; проверка AnswerScoringService по commit cca508ce642c55369d541daf01d4d78e9f91acf1, файл добавлен в 7a35337eac03bead139be17004e38201c813fec9.",
)

doc.save(report)
print(report)
