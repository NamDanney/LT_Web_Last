document.addEventListener('DOMContentLoaded', function () {

    // --- Image Preview ---
    const imageFileInput = document.getElementById('ImageFile');
    if (imageFileInput) {
        imageFileInput.addEventListener('change', function (event) {
            const [file] = event.target.files;
            if (file) {
                const previewWrapper = document.getElementById('new-image-preview-wrapper');
                const previewImg = document.getElementById('new-image-preview');

                previewImg.src = URL.createObjectURL(file);
                previewWrapper.style.display = 'block';

                previewImg.onload = () => {
                    URL.revokeObjectURL(previewImg.src); // free memory
                }
            }
        });
    }

    // --- Auto-update Destination Name ---
    const locationSelect = document.getElementById('SelectedLocationIds');
    const destinationInput = document.getElementById('Destination');
    if (locationSelect && destinationInput) {
        locationSelect.addEventListener('change', function () {
            const selectedOptions = Array.from(locationSelect.selectedOptions);
            const destinationNames = selectedOptions.map(opt => opt.textContent.trim());

            if (destinationNames.length > 0) {
                destinationInput.value = "Tour " + destinationNames.join(' | ');
            } else {
                destinationInput.value = '';
            }
        });
        // Trigger change on load to set initial value if locations are pre-selected
        locationSelect.dispatchEvent(new Event('change'));
    }


    // --- Generic Add/Remove Logic ---
    function setupAddButton(buttonId, containerId, templateId) {
        const addButton = document.getElementById(buttonId);
        if (addButton) {
            addButton.addEventListener('click', function () {
                const container = document.getElementById(containerId);
                const template = document.getElementById(templateId);
                const index = container.children.length;

                const clone = template.content.cloneNode(true);
                const newHtml = clone.firstElementChild.outerHTML.replace(/__index__/g, index);

                container.insertAdjacentHTML('beforeend', newHtml);
            });
        }
    }

    window.removeItem = function (button) {
        const itemToRemove = button.closest('.edit-tour-highlight-input, .item-input, .activity-input');
        if (itemToRemove) {
            const container = itemToRemove.parentElement;
            itemToRemove.remove();
            reindexContainer(container);
        }
    }

    function reindexContainer(container) {
        if (!container || !container.children.length) return;

        const firstInput = container.querySelector('input, textarea, select');
        if (!firstInput) return;

        const baseName = firstInput.name.replace(/\[\d+\].*/, '');

        Array.from(container.children).forEach((item, index) => {
            const inputs = item.querySelectorAll('input, textarea, select');
            inputs.forEach(input => {
                const suffix = input.name.substring(input.name.indexOf(']') + 1);
                const newName = `${baseName}[${index}]${suffix}`;
                input.name = newName;
                input.id = newName;
            });
        });
    }

    setupAddButton('add-highlight-btn', 'highlights-container', 'highlight-template');
    setupAddButton('add-include-btn', 'includes-container', 'include-template');
    setupAddButton('add-exclude-btn', 'excludes-container', 'exclude-template');
    setupAddButton('add-note-btn', 'notes-container', 'note-template');


    // --- Schedule Specific Logic ---
    const addScheduleDayBtn = document.getElementById('add-schedule-day-btn');
    if (addScheduleDayBtn) {
        addScheduleDayBtn.addEventListener('click', function () {
            const container = document.getElementById('schedule-container');
            const template = document.getElementById('schedule-day-template');
            const dayIndex = container.children.length;

            let newHtml = template.innerHTML
                .replace(/__dayIndex__/g, dayIndex)
                .replace(/__dayNumber__/g, dayIndex + 1);

            const tempDiv = document.createElement('div');
            tempDiv.innerHTML = newHtml;
            const newDayElement = tempDiv.firstElementChild;

            container.appendChild(newDayElement);

            const addActivityBtn = newDayElement.querySelector('.add-activity-btn');
            addActivityBtn.addEventListener('click', addActivityHandler);

            // Add an initial activity to the new day
            addActivityBtn.click();
        });
    }

    document.querySelectorAll('.add-activity-btn').forEach(btn => {
        btn.addEventListener('click', addActivityHandler);
    });

    function addActivityHandler(event) {
        const dayDiv = event.target.closest('.schedule-day');
        const activitiesContainer = dayDiv.querySelector('.activities-container');
        const activityTemplate = document.getElementById('activity-template');

        const dayIndex = Array.from(dayDiv.parentElement.children).indexOf(dayDiv);
        const activityIndex = activitiesContainer.children.length;

        let newHtml = activityTemplate.innerHTML
            .replace(/__dayIndex__/g, dayIndex)
            .replace(/__activityIndex__/g, activityIndex);

        activitiesContainer.insertAdjacentHTML('beforeend', newHtml);
    }

    window.removeScheduleDay = function (button) {
        const dayToRemove = button.closest('.schedule-day');
        if (dayToRemove) {
            const container = dayToRemove.parentElement;
            dayToRemove.remove();
            reindexScheduleDays(container);
        }
    }

    function reindexScheduleDays(container) {
        Array.from(container.children).forEach((day, dayIndex) => {
            day.querySelector('h4').textContent = `Ngày ${dayIndex + 1}`;

            const hiddenDayInput = day.querySelector('input[name*=".Day"]');
            if (hiddenDayInput) {
                hiddenDayInput.value = `Ngày ${dayIndex + 1}`;
            }

            const inputs = day.querySelectorAll('input, textarea');
            inputs.forEach(input => {
                input.name = input.name.replace(/Schedules\[\d+\]/, `Schedules[${dayIndex}]`);
            });

            const activitiesContainer = day.querySelector('.activities-container');
            if (activitiesContainer) {
                Array.from(activitiesContainer.children).forEach((activity, activityIndex) => {
                    const activityInput = activity.querySelector('input');
                    if (activityInput) {
                        activityInput.name = `Schedules[${dayIndex}].Activities[${activityIndex}]`;
                    }
                });
            }
        });
    }

});
