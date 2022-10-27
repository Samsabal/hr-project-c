import axios from "axios";
import { createTicketType, userType } from "../../utils/types";

const API_URL = "https://localhost:7295/api/ticket/";

const createTicket = async (ticket: createTicketType, user: userType) => {
    const response = await axios.post(
        API_URL + "CreateTicket",
        {
            phoneNumber: ticket.phoneNumber,
            issue: ticket.issue,
            actionExpected: ticket.actionExpected,
            actionPerformed: ticket.actionPerformed,
            extraInfo: ticket.extraInfo,
            machineId: ticket.machine.machineId,
            creatorId: user.id,
            companyId: user.company.id,
        },
        {
            headers: {
                Authorization: `Bearer ${user.accessToken}`,
            },
        }
    );
    return response;
};

const getTicket = async (ticketId: string, accessToken: string) => {
    const response = await axios.get(API_URL + "GetTicket/" + ticketId, {
        headers: {
            Authorization: `Bearer ${accessToken}`,
        },
    });
    return response;
};

const TicketService = {
    getTicket,
    createTicket,
};

export default TicketService;
