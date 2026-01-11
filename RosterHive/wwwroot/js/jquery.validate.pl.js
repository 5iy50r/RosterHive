(function ($) {
    if (!$.validator) return;

    $.extend($.validator.messages, {
        required: "To pole jest wymagane.",
        remote: "Popraw to pole.",
        email: "Podaj poprawny adres e-mail.",
        url: "Podaj poprawny adres URL.",
        date: "Podaj poprawną datę.",
        dateISO: "Podaj poprawną datę (RRRR-MM-DD).",
        number: "Podaj poprawną liczbę.",
        digits: "Wpisz same cyfry.",
        creditcard: "Podaj poprawny numer karty.",
        equalTo: "Wpisz ponownie tę samą wartość.",
        extension: "Podaj wartość z poprawnym rozszerzeniem.",
        maxlength: $.validator.format("Wpisz maksymalnie {0} znaków."),
        minlength: $.validator.format("Wpisz co najmniej {0} znaków."),
        rangelength: $.validator.format("Wpisz wartość o długości od {0} do {1} znaków."),
        range: $.validator.format("Wpisz wartość z zakresu od {0} do {1}."),
        max: $.validator.format("Wpisz wartość mniejszą lub równą {0}."),
        min: $.validator.format("Wpisz wartość większą lub równą {0}.")
    });
})(jQuery);
