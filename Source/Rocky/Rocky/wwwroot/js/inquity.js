var datTable;

$(document).ready(function () {

    loadDataTable("GetInquiryList");
});

function loadDataTable(url) {
    datTable = $('#tblData').DataTable({
        "ajax": {
            "url": "/inquiry/" + url
        },
        "columns": [
            { "data": "id", "widht": "10%" },
            { "data": "fullName", "widht": "15%" },
            { "data": "phoneNumber", "widht": "15%" },
            { "data": "email", "widht": "15%" },
            {
                "data": "id", 
                "render": function (data) {
                    return `
                            <div class="text-center">
                                <a href="/Inquiry/Details/${data}" class="btn btn-success text-white" style="cursor:pointer">
                                    <i class="fas fa-edit"></i> 
                                </a>
                            </div>
                           `;
                },
                "width": "5%"
            }
        ]
    });
}