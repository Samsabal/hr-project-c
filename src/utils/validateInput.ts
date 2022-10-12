/**
 * Returns an error if a given phoneNumber doesn't meet the specified phone number requirements
 * 
 * @param phoneNumber - The phone number to validate
 * @returns The error which occured, if any
 */
export const validatePhoneNumber = (phoneNumber: string): string | undefined => {
    let error;
    if(!phoneNumber) {
        error = "This field is required";
    } else if (!/^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{3}[-\s\.]?[0-9]{4,6}$/i.test(phoneNumber.split(" ").join(""))) {
        error = 'Invalid phone number';
    }
    return error;
}

/**
 * Returns an error if a given string isn't long enough
 * 
 * @param text - The text from the textarea to validate
 * @returns The error which occured, if any
 */
 export const validateTextarea = (text: string): string | undefined => {
    let error;
    if(!text) {
        error = "This field is required";
    } else if (text.length < 25) {
        error = "Your input is too short";
    }
    return error;
}