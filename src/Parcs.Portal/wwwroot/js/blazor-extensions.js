window.blazorExtensions = {
    SET_AUTHN_FAILED : function () {
        document.getElementById('login-error-wrapper').style.display = 'block';
        document.getElementById('account-email-input').style.borderColor = 'red';
        document.getElementById('account-password-input').style.borderColor = 'red';
    },

    CLEAR_AUTHN_FAILED : function () {
        document.getElementById('login-error-wrapper').style.display = 'none';
        document.getElementById('account-email-input').style.borderColor = null;
        document.getElementById('account-password-input').style.borderColor = null;
    },

    SET_CHANGE_PASSWORD_CURRENT_FAILED: function () {
        document.getElementById('changepassword-error-current').style.display = 'block';
    },

    CLEAR_CHANGE_PASSWORD_CURRENT_FAILED: function () {
        document.getElementById('changepassword-error-current').style.display = 'none';
    },

    SET_CHANGE_INVALID_PHONE_NUMBER: function () {
        document.getElementById('phonenumber-error').style.display = 'block';
    },

    CLEAR_CHANGE_INVALID_PHONE_NUMBER: function () {
        document.getElementById('phonenumber-error').style.display = 'none';
    },

    SET_CHANGE_PASSWORD_CONFIRM_FAILED: function () {
        document.getElementById('changepassword-error-confirm').style.display = 'block';
    },

    CLEAR_CHANGE_PASSWORD_CONFIRM_FAILED: function () {
        document.getElementById('changepassword-error-confirm').style.display = 'none';
    },

    CHECK_PHONE_NUMBER: function (number) {
        number = number.replace(/[\s\-]/g, '');
        return number.match(/^((\+?3)?8)?((0\(\d{2}\)?)|(\(0\d{2}\))|(0\d{2}))\d{7}$/) != null;
    },

    DISABLE_LOGIN_BUTTON: function () {
        document.getElementById('login-submit-button').disabled = true;
    },

    ENABLE_LOGIN_BUTTON: function () {
        document.getElementById('login-submit-button').disabled = false;
    },

    LOGIN: function (login, password) {
        var data = {
            "Email": login,
            "Password": password
        };

        $.ajax({
            url: "localapi/login",
            type: 'POST',
            async: false,  
            contentType: "application/json; charset=utf-8",
            data: JSON.stringify(data),
            processData: false,
            success: function (resultData) {
                window.location.href = "/main";
            }
        });
    },

    LOGOUT: function () {
        $.ajax({
            url: "localapi/logout",
            type: 'POST',
            contentType: "application/json; charset=utf-8",
            processData: false,
            success: function (resultData) {
                window.location.href = "/signin";
            }
        });
    },

    SET_SELECT2: function () {
        $('.select').select2({
            minimumResultsForSearch: -1,
            width: '100%'
        });
    },

    ONCHANGE_SELECT2: function (id, dotnetHelper, nameFunc) {
        $('#' + id).on('select2:select', function (e) {
            dotnetHelper.invokeMethodAsync(nameFunc, $('#' + id).val());
        });
    },

    RESET_SIDEBAR_OPENED: function () {
        document.getElementById('sidebar').removeAttribute('style');
        $('#mobile_btn').removeClass('opened');
        $('html').removeClass('menu-opened');
        var $wrapper = $('.main-wrapper');
        $wrapper.removeClass('slide-nav');
    },

    REFRESH_IMAGE: function (imgElement, imgURL) {
        var timestamp = new Date().getTime();

        var el = document.getElementById(imgElement);   

        var queryString = "?t=" + timestamp;

        el.src = imgURL + queryString;
    },

    TOGGLE_MODAL: function (modalId) {
        console.log(modalId);
        $('#' + modalId).modal('toggle');
    },

    CHANGE_PLAN_STATUS_SUCCESS: function (index) {
        $('#plan-status-image-' + index).removeClass();
        $('#plan-status-image-' + index).addClass("fa fa-check");
        $('#plan-status-' + index).text("Виконано ");
        //$('#plan-button-' + index).attr('disabled', true);
        $('#plan-button-' + index).addClass('hide');
    },

    HISTORY_BACK: function () {
        history.back();
    },

    SET_STUDY_CHART_BUTTONS: function (index, buttonId) {
        for (i = 1; i <= 3; i++) {
            $('#study-btn-' + index + '-' + i).removeClass();
        }

        for (i = 1; i <= 3; i++) {
            if (i == buttonId) {
                document.getElementById('study-btn-' + index + '-' + i).classList.add('btn');
                document.getElementById('study-btn-' + index + '-' + i).classList.add('btn-primary');
                document.getElementById('study-btn-' + index + '-' + i).classList.add('active');
            }
            else {
                document.getElementById('study-btn-' + index + '-' + i).classList.add('btn');
                document.getElementById('study-btn-' + index + '-' + i).classList.add('btn-white');
            }
        }
    }
}



