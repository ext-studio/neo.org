﻿$(function () {
    //根据年份显示日期 - Show date by year
    $(".year").click(function () {
        $(".blog_date ul").hide(300);
        $(this.parentNode).find("ul").show(300);
    });

    $('.back-to-top').click(function (event) {
        $('html, body').animate({ scrollTop: 0 }, 500);
    })

    $("#blogDate").find("ul:first").show(300);
})